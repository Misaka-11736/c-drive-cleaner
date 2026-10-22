using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace CCleaner.Disks
{
    public class DedupeFile
    {
        public string Path = "";
        public string Volume = "";
        public long Size;
        public DateTime Modified;
        public string Id = "";
        public uint Links;
        public string Head = "";
        public string Full = "";
        public string Note = "";

        public bool IsHardlink
        {
            get { return Links > 1; }
        }
    }

    public class DedupeGroup
    {
        public long Size;
        public List<DedupeFile> Files = new List<DedupeFile>();
        public int DistinctIds;
        public long Reclaimable;
        public bool HardlinkOnly;
        public DedupeFile Keep;
        public string Reason = "";

        public string VolumeList
        {
            get
            {
                List<string> vols = new List<string>();
                foreach (DedupeFile f in Files)
                {
                    string v = f.Volume + ":";
                    if (!vols.Contains(v)) vols.Add(v);
                }
                vols.Sort();
                return string.Join(", ", vols.ToArray());
            }
        }
    }

    public class DedupeProgress
    {
        public string Phase = "";
        public long Files;
        public long Bytes;
        public string Current = "";
        public int Groups;
    }

    public class DedupeResult
    {
        public List<DedupeGroup> Groups = new List<DedupeGroup>();
        public long TotalFiles;
        public long TotalBytes;
        public long CandidateFiles;
        public long ReclaimableBytes;
        public int HardlinkGroups;
        public int DeniedCount;
        public List<string> Denied = new List<string>();
        public bool Canceled;
        public double Seconds;
    }

    public class AllVolumeDeduper
    {
        public const long MinSize = 1024L * 1024;

        private static readonly string[] SkipNames =
        {
            "System Volume Information", "$Recycle.Bin", "$WinREAgent", "Config.Msi",
            "$Windows.~BT", "$Windows.~WS"
        };

        private readonly string[] _roots;

        public AllVolumeDeduper()
        {
        }

        public AllVolumeDeduper(string[] roots)
        {
            _roots = roots;
        }

        public DedupeResult Scan(IProgress<DedupeProgress> progress, CancellationToken ct)
        {
            Stopwatch sw = Stopwatch.StartNew();
            DedupeResult r = new DedupeResult();

            string[] roots = _roots != null && _roots.Length > 0 ? _roots : BuildDefaultRoots();
            Dictionary<long, List<DedupeFile>> bySize = new Dictionary<long, List<DedupeFile>>();
            Stopwatch report = Stopwatch.StartNew();

            for (int i = 0; i < roots.Length; i++)
            {
                if (ct.IsCancellationRequested) { r.Canceled = true; break; }
                if (!Directory.Exists(roots[i])) continue;

                Report(progress, "索引 " + roots[i], r, "", 0);
                IndexRoot(roots[i], r, bySize, progress, ct, ref report);
            }

            if (!r.Canceled)
            {
                Report(progress, "查重", r, "", 0);
                BuildGroups(r, bySize, progress, ct);
            }

            r.ReclaimableBytes = 0;
            foreach (DedupeGroup g in r.Groups) r.ReclaimableBytes += g.Reclaimable;

            r.Groups.Sort(delegate(DedupeGroup a, DedupeGroup b)
            {
                if (a.HardlinkOnly != b.HardlinkOnly) return a.HardlinkOnly ? 1 : -1;
                return b.Reclaimable.CompareTo(a.Reclaimable);
            });

            r.Seconds = sw.Elapsed.TotalSeconds;
            return r;
        }

        private static string[] BuildDefaultRoots()
        {
            List<string> roots = new List<string>();
            string sys = Path.GetPathRoot(Environment.SystemDirectory);
            string users = Path.Combine(sys, "Users");
            if (Directory.Exists(users)) roots.Add(users);

            try
            {
                foreach (VolumeInfo v in VolumeUtil.GetVolumes())
                {
                    if (!roots.Contains(v.Root)) roots.Add(v.Root);
                }
            }
            catch { }
            return roots.ToArray();
        }

        private void IndexRoot(string root, DedupeResult r, Dictionary<long, List<DedupeFile>> bySize,
            IProgress<DedupeProgress> progress, CancellationToken ct, ref Stopwatch report)
        {
            string volume = Path.GetPathRoot(root).Substring(0, 1).ToUpperInvariant();
            Stack<string> stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                if (ct.IsCancellationRequested) { r.Canceled = true; return; }

                string dir = stack.Pop();
                FileSystemInfo[] entries;
                try { entries = new DirectoryInfo(dir).GetFileSystemInfos(); }
                catch
                {
                    r.DeniedCount++;
                    if (r.Denied.Count < 200) r.Denied.Add(dir);
                    continue;
                }

                foreach (FileSystemInfo e in entries)
                {
                    try
                    {
                        if (((int)e.Attributes & 1024) != 0) continue;

                        DirectoryInfo di = e as DirectoryInfo;
                        if (di != null)
                        {
                            if (IsSkipped(di.Name)) continue;
                            stack.Push(di.FullName);
                            continue;
                        }

                        FileInfo fi = (FileInfo)e;
                        r.TotalFiles++;
                        r.TotalBytes += fi.Length;

                        if (fi.Length < MinSize) continue;
                        r.CandidateFiles++;

                        DedupeFile f = new DedupeFile();
                        f.Path = fi.FullName;
                        f.Volume = volume;
                        f.Size = fi.Length;
                        f.Modified = fi.LastWriteTime;

                        List<DedupeFile> list;
                        if (!bySize.TryGetValue(f.Size, out list))
                        {
                            list = new List<DedupeFile>();
                            bySize[f.Size] = list;
                        }
                        list.Add(f);
                    }
                    catch { }
                }

                if (report.ElapsedMilliseconds >= 250)
                {
                    report.Restart();
                    Report(progress, "索引 " + root, r, dir, 0);
                }
            }
        }

        private static bool IsSkipped(string name)
        {
            foreach (string s in SkipNames)
                if (string.Equals(name, s, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void BuildGroups(DedupeResult r, Dictionary<long, List<DedupeFile>> bySize,
            IProgress<DedupeProgress> progress, CancellationToken ct)
        {
            Stopwatch report = Stopwatch.StartNew();
            Dictionary<DedupeFile, bool> inContent = new Dictionary<DedupeFile, bool>();

            foreach (KeyValuePair<long, List<DedupeFile>> kv in bySize)
            {
                if (ct.IsCancellationRequested) { r.Canceled = true; return; }
                if (kv.Value.Count < 2) continue;

                Dictionary<string, List<DedupeFile>> byId = new Dictionary<string, List<DedupeFile>>(StringComparer.Ordinal);
                foreach (DedupeFile f in kv.Value)
                {
                    string id;
                    uint links;
                    if (!TryIdentity(f.Path, out id, out links))
                    {
                        r.DeniedCount++;
                        if (r.Denied.Count < 200) r.Denied.Add(f.Path);
                        continue;
                    }

                    f.Id = id;
                    f.Links = links;

                    List<DedupeFile> same;
                    if (!byId.TryGetValue(id, out same))
                    {
                        same = new List<DedupeFile>();
                        byId[id] = same;
                    }
                    same.Add(f);
                }

                List<DedupeFile> reps = new List<DedupeFile>();
                foreach (KeyValuePair<string, List<DedupeFile>> idGroup in byId)
                {
                    reps.Add(idGroup.Value[0]);
                    if (idGroup.Value.Count > 1)
                    {
                        foreach (DedupeFile f in idGroup.Value) f.Note = "硬链接";
                    }
                }

                Dictionary<string, List<DedupeFile>> byHead = new Dictionary<string, List<DedupeFile>>(StringComparer.Ordinal);
                foreach (DedupeFile rep in reps)
                {
                    if (ct.IsCancellationRequested) { r.Canceled = true; return; }
                    string head = HashFile(rep.Path, 65536);
                    if (head == null)
                    {
                        r.DeniedCount++;
                        if (r.Denied.Count < 200) r.Denied.Add(rep.Path);
                        continue;
                    }
                    rep.Head = head;

                    List<DedupeFile> list;
                    if (!byHead.TryGetValue(head, out list))
                    {
                        list = new List<DedupeFile>();
                        byHead[head] = list;
                    }
                    list.Add(rep);
                }

                foreach (KeyValuePair<string, List<DedupeFile>> headGroup in byHead)
                {
                    if (headGroup.Value.Count < 2) continue;

                    Dictionary<string, List<DedupeFile>> byFull = new Dictionary<string, List<DedupeFile>>(StringComparer.Ordinal);
                    foreach (DedupeFile rep in headGroup.Value)
                    {
                        if (ct.IsCancellationRequested) { r.Canceled = true; return; }

                        DateTime before = rep.Modified;
                        string full = HashFile(rep.Path, 0);
                        if (full == null)
                        {
                            r.DeniedCount++;
                            if (r.Denied.Count < 200) r.Denied.Add(rep.Path);
                            continue;
                        }

                        DateTime after;
                        try { after = File.GetLastWriteTime(rep.Path); }
                        catch { after = before; }

                        if (after != before)
                        {
                            continue;
                        }

                        rep.Full = full;

                        List<DedupeFile> list;
                        if (!byFull.TryGetValue(full, out list))
                        {
                            list = new List<DedupeFile>();
                            byFull[full] = list;
                        }
                        list.Add(rep);
                    }

                    foreach (KeyValuePair<string, List<DedupeFile>> fullGroup in byFull)
                    {
                        if (fullGroup.Value.Count < 2) continue;

                        DedupeGroup g = new DedupeGroup();
                        g.Size = kv.Key;
                        g.DistinctIds = fullGroup.Value.Count;

                        foreach (DedupeFile rep in fullGroup.Value)
                        {
                            List<DedupeFile> sameId;
                            if (byId.TryGetValue(rep.Id, out sameId))
                            {
                                foreach (DedupeFile f in sameId)
                                {
                                    g.Files.Add(f);
                                    inContent[f] = true;
                                }
                            }
                            else
                            {
                                g.Files.Add(rep);
                                inContent[rep] = true;
                            }
                        }

                        g.Reclaimable = g.Size * (g.DistinctIds - 1);
                        Suggest(g);
                        r.Groups.Add(g);
                    }

                    if (report.ElapsedMilliseconds >= 250)
                    {
                        report.Restart();
                        Report(progress, "查重", r, "", r.Groups.Count);
                    }
                }

                if (report.ElapsedMilliseconds >= 250)
                {
                    report.Restart();
                    Report(progress, "查重", r, "", r.Groups.Count);
                }
            }

            // 仅由硬链接构成、且没有参与任何内容重复组的，单独列出
            foreach (KeyValuePair<long, List<DedupeFile>> kv in bySize)
            {
                Dictionary<string, List<DedupeFile>> byId = new Dictionary<string, List<DedupeFile>>(StringComparer.Ordinal);
                foreach (DedupeFile f in kv.Value)
                {
                    if (inContent.ContainsKey(f)) continue;
                    if (string.IsNullOrEmpty(f.Id)) continue;

                    List<DedupeFile> same;
                    if (!byId.TryGetValue(f.Id, out same))
                    {
                        same = new List<DedupeFile>();
                        byId[f.Id] = same;
                    }
                    same.Add(f);
                }

                foreach (KeyValuePair<string, List<DedupeFile>> idGroup in byId)
                {
                    if (idGroup.Value.Count < 2) continue;

                    DedupeGroup g = new DedupeGroup();
                    g.Size = kv.Key;
                    g.DistinctIds = 1;
                    g.HardlinkOnly = true;
                    g.Reclaimable = 0;
                    g.Reason = "硬链接：删除任一路径都不释放空间";
                    foreach (DedupeFile f in idGroup.Value)
                    {
                        f.Note = "硬链接";
                        g.Files.Add(f);
                    }
                    r.HardlinkGroups++;
                    r.Groups.Add(g);
                }
            }
        }

        private static void Suggest(DedupeGroup g)
        {
            DedupeFile best = null;
            int bestScore = int.MinValue;
            string reason = "";

            foreach (DedupeFile f in g.Files)
            {
                int score = 0;
                string r = "";

                if (IsAppPath(f.Path))
                {
                    score += 1000;
                    r = "应用/游戏目录优先";
                }

                if (string.Equals(f.Volume, "F", StringComparison.OrdinalIgnoreCase))
                {
                    score += 100;
                    if (r.Length == 0) r = "备份盘优先";
                }

                if (f.Links > 1) score -= 500;
                score -= f.Path.Length / 20;

                if (best == null || score > bestScore ||
                    (score == bestScore && f.Modified > best.Modified))
                {
                    best = f;
                    bestScore = score;
                    reason = r;
                }
            }

            g.Keep = best;
            if (string.IsNullOrEmpty(reason)) reason = "较新/路径较短";
            g.Reason = reason;
        }

        private static bool IsAppPath(string path)
        {
            string p = path.ToLowerInvariant();
            return p.Contains("\\program files\\") || p.Contains("\\program files (x86)\\") ||
                   p.Contains("\\steamlibrary\\") || p.Contains("\\steamapps\\") ||
                   p.Contains("\\epic games\\") || p.Contains("\\xboxgames\\") ||
                   p.Contains("\\gog games\\") || p.Contains("\\games\\");
        }

        // ---------------------------------------------------------- 文件身份与哈希
        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint Low;
            public uint High;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            public uint FileAttributes;
            public FILETIME CreationTime;
            public FILETIME LastAccessTime;
            public FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle hFile,
            out BY_HANDLE_FILE_INFORMATION info);

        private static bool TryIdentity(string path, out string id, out uint links)
        {
            id = "";
            links = 1;
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.None))
                {
                    BY_HANDLE_FILE_INFORMATION info;
                    if (!GetFileInformationByHandle(fs.SafeFileHandle, out info)) return false;

                    ulong index = ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow;
                    id = info.VolumeSerialNumber.ToString("X8") + ":" + index.ToString("X16");
                    links = info.NumberOfLinks;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string HashFile(string path, long limit)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.SequentialScan))
                using (SHA256 sha = SHA256.Create())
                using (CryptoStream cs = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
                {
                    byte[] buffer = new byte[65536];
                    long remaining = limit > 0 ? limit : long.MaxValue;

                    while (remaining > 0)
                    {
                        int want = (int)Math.Min(buffer.Length, remaining);
                        int read = fs.Read(buffer, 0, want);
                        if (read <= 0) break;
                        cs.Write(buffer, 0, read);
                        remaining -= read;
                    }
                    cs.FlushFinalBlock();
                    return BitConverter.ToString(sha.Hash).Replace("-", "");
                }
            }
            catch
            {
                return null;
            }
        }

        private static void Report(IProgress<DedupeProgress> progress, string phase,
            DedupeResult r, string current, int groups)
        {
            if (progress == null) return;
            DedupeProgress p = new DedupeProgress();
            p.Phase = phase;
            p.Files = r.TotalFiles;
            p.Bytes = r.TotalBytes;
            p.Current = current;
            p.Groups = groups;
            progress.Report(p);
        }
    }
}
