using System;
using System.Collections.Generic;
using System.IO;
using System.Management;

namespace CCleaner.Disks
{
    public static class VolumeUtil
    {
        public static List<VolumeInfo> GetVolumes()
        {
            List<VolumeInfo> list = new List<VolumeInfo>();
            Dictionary<string, string> bus = GetBusTypes();
            Dictionary<string, ManagementObject> disks = GetLogicalDisks();
            string sysRoot = Path.GetPathRoot(Environment.SystemDirectory);

            try
            {
                foreach (DriveInfo d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!d.IsReady) continue;
                        if (d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable) continue;
                        if (string.Equals(d.Name, sysRoot, StringComparison.OrdinalIgnoreCase)) continue;

                        VolumeInfo v = new VolumeInfo();
                        v.Root = d.Name;
                        v.Letter = d.Name.Substring(0, 1);
                        v.Total = d.TotalSize;
                        v.Free = d.AvailableFreeSpace;
                        v.DriveType = (int)d.DriveType;
                        v.IsRemovable = d.DriveType == DriveType.Removable;
                        try { v.Label = d.VolumeLabel; } catch { }
                        try { v.FileSystem = d.DriveFormat; } catch { }

                        ManagementObject mo;
                        if (disks.TryGetValue(v.Letter + ":", out mo))
                        {
                            v.Serial = Convert.ToString(mo["VolumeSerialNumber"]);
                            string label = Convert.ToString(mo["VolumeName"]);
                            if (!string.IsNullOrEmpty(label)) v.Label = label;
                            string fs = Convert.ToString(mo["FileSystem"]);
                            if (!string.IsNullOrEmpty(fs)) v.FileSystem = fs;
                        }

                        string bt;
                        if (bus.TryGetValue(v.Letter, out bt)) v.BusType = bt;
                        v.IsExternal = v.IsRemovable || string.Equals(v.BusType, "USB", StringComparison.OrdinalIgnoreCase);
                        v.IsBackup = LooksLikeBackup(v);
                        list.Add(v);
                    }
                    catch { }
                }
            }
            catch { }

            list.Sort(delegate(VolumeInfo a, VolumeInfo b)
            {
                return string.Compare(a.Letter, b.Letter, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        private static Dictionary<string, ManagementObject> GetLogicalDisks()
        {
            Dictionary<string, ManagementObject> map = new Dictionary<string, ManagementObject>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk"))
                {
                    foreach (ManagementBaseObject o in s.Get())
                    {
                        ManagementObject mo = o as ManagementObject;
                        if (mo == null) continue;
                        string id = Convert.ToString(mo["DeviceID"]);
                        if (!string.IsNullOrEmpty(id)) map[id] = mo;
                    }
                }
            }
            catch { }
            return map;
        }

        private static Dictionary<string, string> GetBusTypes()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
                {
                    foreach (ManagementBaseObject o in s.Get())
                    {
                        ManagementObject disk = o as ManagementObject;
                        if (disk == null) continue;

                        string iface = Convert.ToString(disk["InterfaceType"]);
                        if (string.IsNullOrEmpty(iface)) continue;

                        try
                        {
                            foreach (ManagementBaseObject p in disk.GetRelated("Win32_DiskPartition"))
                            {
                                ManagementObject part = p as ManagementObject;
                                if (part == null) continue;
                                foreach (ManagementBaseObject l in part.GetRelated("Win32_LogicalDisk"))
                                {
                                    string id = Convert.ToString(((ManagementObject)l)["DeviceID"]);
                                    if (!string.IsNullOrEmpty(id) && id.Length >= 2)
                                        map[id.Substring(0, 1)] = iface;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return map;
        }

        public static bool LooksLikeBackup(VolumeInfo v)
        {
            try
            {
                if (Directory.Exists(Path.Combine(v.Root, "WindowsImageBackup"))) return true;
                if (Directory.Exists(Path.Combine(v.Root, "FileHistory"))) return true;
            }
            catch { }

            string label = (v.Label ?? "").ToLowerInvariant();
            return label.Contains("backup") || label.Contains("passport") || label.Contains("time machine") ||
                   label.Contains("备份");
        }

        /// <summary>删除前的卷内白名单校验。</summary>
        public static bool IsSafeVolumePath(string path, VolumeInfo v)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (v == null || v.IsBackup) return false;
            if (!path.StartsWith(v.Root, StringComparison.OrdinalIgnoreCase)) return false;

            string n = path.TrimEnd('\\');
            if (n.Length <= 3) return false;                       // 卷根
            if (n.Contains("..")) return false;
            if (n.IndexOfAny(new char[] { '*', '?' }) >= 0) return false;

            string rel = n.Substring(3);
            string[] parts = rel.Split('\\');
            foreach (string p in parts)
            {
                string low = p.ToLowerInvariant();
                if (low == "system volume information") return false;
                if (low == "$recycle.bin") return false;
                if (low == "efi" || low == "boot" || low == "windows" ||
                    low == "program files" || low == "program files (x86)" ||
                    low == "programdata" || low == "recovery") return false;
            }

            string last = System.IO.Path.GetFileName(n).ToLowerInvariant();
            if (last == "pagefile.sys" || last == "hiberfil.sys" || last == "swapfile.sys" ||
                last == "dumpstack.log.tmp" || last == "bootmgr" || last == "bootnxt") return false;

            if (HasReparseAncestor(n, v.Root)) return false;

            try
            {
                DirectoryInfo di = new DirectoryInfo(n);
                if (!di.FullName.StartsWith(v.Root, StringComparison.OrdinalIgnoreCase)) return false;
            }
            catch { }

            return true;
        }

        public static bool HasReparseAncestor(string path, string root)
        {
            try
            {
                string cur = Path.GetDirectoryName(path);
                int guard = 0;
                while (!string.IsNullOrEmpty(cur) && guard++ < 64)
                {
                    if (cur.Length < 3) break;
                    try
                    {
                        FileAttributes a = File.GetAttributes(cur);
                        if ((a & FileAttributes.ReparsePoint) != 0) return true;
                    }
                    catch { }

                    string withSlash = cur.TrimEnd('\\') + "\\";
                    if (string.Equals(withSlash, root, StringComparison.OrdinalIgnoreCase)) break;
                    string parent = Path.GetDirectoryName(cur);
                    if (string.Equals(parent, cur, StringComparison.OrdinalIgnoreCase)) break;
                    cur = parent;
                }
            }
            catch { }
            return false;
        }

        public static void Measure(string path, out long bytes, out long files)
        {
            bytes = 0;
            files = 0;
            if (!Directory.Exists(path)) return;

            Stack<string> stack = new Stack<string>();
            stack.Push(path);
            while (stack.Count > 0)
            {
                string p = stack.Pop();
                FileSystemInfo[] entries;
                try { entries = new DirectoryInfo(p).GetFileSystemInfos(); }
                catch { continue; }

                foreach (FileSystemInfo e in entries)
                {
                    try
                    {
                        if (((int)e.Attributes & 1024) != 0) continue;
                        DirectoryInfo di = e as DirectoryInfo;
                        if (di != null) stack.Push(di.FullName);
                        else
                        {
                            bytes += ((FileInfo)e).Length;
                            files++;
                        }
                    }
                    catch { }
                }
            }
        }
    }
}
