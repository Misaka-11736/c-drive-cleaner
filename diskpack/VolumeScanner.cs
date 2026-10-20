using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace CCleaner.Disks
{
    public class VolumeScanner
    {
        private const long TopFileMin = 200L * 1024 * 1024;
        private const long DupMin = 50L * 1024 * 1024;
        private const long InstallerMin = 100L * 1024 * 1024;
        private const int DupCandidateMax = 3000;

        private class Watch
        {
            public string Id;
            public string Category;
            public string Label;
            public string Root;
            public VolumeItemMode Mode;
            public bool Deletable;
            public RiskLevel Risk;
            public string Note;
            public long Bytes;
            public long Files;
            public long InstallerBytes;
            public int InstallerCount;
        }

        private class Node
        {
            public string Path;
            public int Depth;
            public Watch Watch;
            public string Top;
        }

        public VolumeScanResult Scan(VolumeInfo vol, bool deep, IProgress<ScanProgress> progress, CancellationToken ct)
        {
            Stopwatch sw = Stopwatch.StartNew();
            VolumeScanResult r = new VolumeScanResult();
            r.Volume = vol;
            r.Deep = deep;

            List<Watch> watches = new List<Watch>();
            Dictionary<string, Watch> map = new Dictionary<string, Watch>(StringComparer.OrdinalIgnoreCase);

            AddRecycle(watches, vol);
            AddSteam(watches, map, vol);
            AddKnownGameDirs(watches, map, vol);
            AddDownloads(watches, map, vol);

            Walk(vol, watches, map, r, deep, progress, ct);

            MeasureRecycle(r, vol);

            BuildItems(r, vol, watches, deep);
            BuildRankings(r, watches);
            r.Seconds = sw.Elapsed.TotalSeconds;
            return r;
        }

        // ---------------------------------------------------------- 目标发现
        private static void AddRecycle(List<Watch> watches, VolumeInfo v)
        {
            Watch w = new Watch();
            w.Id = "RECYCLE";
            w.Category = "回收站";
            w.Label = "回收站";
            w.Root = Path.Combine(v.Root, "$Recycle.Bin");
            w.Mode = VolumeItemMode.Recycle;
            w.Deletable = !v.IsBackup;
            w.Risk = RiskLevel.Low;
            w.Note = "清空该盘的回收站（需要管理员时可从主程序启动）";
            watches.Add(w);
        }

        private static void AddSteam(List<Watch> watches, Dictionary<string, Watch> map, VolumeInfo v)
        {
            List<string> libs = FindDirs(v.Root, "steamapps", 3);
            int n = 0;
            foreach (string lib in libs)
            {
                n++;
                AddWatch(watches, map, lib, "shadercache", "STEAM-CACHE-" + n, "游戏缓存", "Steam 着色器缓存",
                    VolumeItemMode.Contents, true, RiskLevel.Safe, "游戏着色器缓存，可重建");
                AddWatch(watches, map, lib, "downloading", "STEAM-DL-" + n, "游戏缓存", "Steam 未完成下载",
                    VolumeItemMode.Contents, true, RiskLevel.Low, "未完成的下载分块，删除后需重新下载");
                AddWatch(watches, map, lib, "temp", "STEAM-TEMP-" + n, "游戏缓存", "Steam 安装临时",
                    VolumeItemMode.Contents, true, RiskLevel.Low, "安装临时文件");
                AddWatch(watches, map, lib, "common", "STEAM-LIB-" + n, "游戏库", "Steam 游戏库",
                    VolumeItemMode.Report, false, RiskLevel.Medium, "仅统计；建议用 Steam 迁移到其他盘，不要直接删");
            }
        }

        private static void AddKnownGameDirs(List<Watch> watches, Dictionary<string, Watch> map, VolumeInfo v)
        {
            string[] names = { "XboxGames", "Epic Games", "GOG Games", "Battle.net", "Riot Games" };
            int n = 0;
            foreach (string name in names)
            {
                string p = Path.Combine(v.Root, name);
                if (!Directory.Exists(p)) continue;
                n++;
                Watch w = new Watch();
                w.Id = "GAME-" + n;
                w.Category = "游戏库";
                w.Label = name;
                w.Root = p;
                w.Mode = VolumeItemMode.Report;
                w.Deletable = false;
                w.Risk = RiskLevel.Medium;
                w.Note = "仅统计；建议通过启动器或系统设置迁移，不要直接删除";
                watches.Add(w);
                map[p] = w;
            }
        }

        private static void AddDownloads(List<Watch> watches, Dictionary<string, Watch> map, VolumeInfo v)
        {
            string[] names = { "Downloads", "Download", "下载", "迅雷下载", "BT下载" };
            int n = 0;
            foreach (string name in names)
            {
                string p = Path.Combine(v.Root, name);
                if (!Directory.Exists(p)) continue;
                n++;
                Watch w = new Watch();
                w.Id = "DL-" + n;
                w.Category = "下载与安装包";
                w.Label = name;
                w.Root = p;
                w.Mode = VolumeItemMode.Report;
                w.Deletable = false;
                w.Risk = RiskLevel.High;
                w.Note = "仅统计 100 MB 以上、超过 30 天的安装包/镜像；请自行确认后再删";
                watches.Add(w);
                map[p] = w;
            }
        }

        private static void AddWatch(List<Watch> watches, Dictionary<string, Watch> map, string parent, string child,
            string id, string category, string label, VolumeItemMode mode, bool deletable, RiskLevel risk, string note)
        {
            string p = Path.Combine(parent, child);
            if (!Directory.Exists(p)) return;

            Watch w = new Watch();
            w.Id = id;
            w.Category = category;
            w.Label = label;
            w.Root = p;
            w.Mode = mode;
            w.Deletable = deletable;
            w.Risk = risk;
            w.Note = note;
            watches.Add(w);
            map[p] = w;
        }

        public static List<string> FindDirs(string root, string name, int maxDepth)
        {
            List<string> found = new List<string>();
            Queue<Node> queue = new Queue<Node>();

            Node start = new Node();
            start.Path = root;
            start.Depth = 0;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                Node n = queue.Dequeue();
                FileSystemInfo[] entries;
                try { entries = new DirectoryInfo(n.Path).GetFileSystemInfos(); }
                catch { continue; }

                foreach (FileSystemInfo e in entries)
                {
                    try
                    {
                        DirectoryInfo di = e as DirectoryInfo;
                        if (di == null) continue;
                        if (((int)di.Attributes & 1024) != 0) continue;
                        if (string.Equals(di.Name, name, StringComparison.OrdinalIgnoreCase)) found.Add(di.FullName);
                        if (n.Depth + 1 < maxDepth) queue.Enqueue(new Node() { Path = di.FullName, Depth = n.Depth + 1 });
                    }
                    catch { }
                }
            }
            return found;
        }

        // ---------------------------------------------------------- 遍历
        private void Walk(VolumeInfo v, List<Watch> watches, Dictionary<string, Watch> map,
            VolumeScanResult r, bool deep, IProgress<ScanProgress> progress, CancellationToken ct)
        {
            Dictionary<string, long> topBytes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            List<FileItem> candidates = new List<FileItem>();

            HashSet<string> skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            skip.Add(Path.Combine(v.Root, "System Volume Information"));
            skip.Add(Path.Combine(v.Root, "$Recycle.Bin"));
            skip.Add(Path.Combine(v.Root, "$WinREAgent"));
            skip.Add(Path.Combine(v.Root, "Config.Msi"));

            Stack<Node> stack = new Stack<Node>();
            Node rootNode = new Node();
            rootNode.Path = v.Root;
            rootNode.Depth = 0;
            stack.Push(rootNode);

            Stopwatch report = Stopwatch.StartNew();

            while (stack.Count > 0)
            {
                if (ct.IsCancellationRequested) { r.Canceled = true; break; }

                Node n = stack.Pop();
                if (skip.Contains(n.Path)) continue;

                Watch watch = n.Watch;
                Watch mapped;
                if (map.TryGetValue(n.Path, out mapped)) watch = mapped;

                FileSystemInfo[] entries;
                try { entries = new DirectoryInfo(n.Path).GetFileSystemInfos(); }
                catch
                {
                    if (r.Denied.Count < 200) r.Denied.Add(n.Path);
                    continue;
                }

                r.DirCount++;

                foreach (FileSystemInfo e in entries)
                {
                    try
                    {
                        if (((int)e.Attributes & 1024) != 0) continue;

                        DirectoryInfo di = e as DirectoryInfo;
                        if (di != null)
                        {
                            Node child = new Node();
                            child.Path = di.FullName;
                            child.Depth = n.Depth + 1;
                            child.Watch = watch;
                            child.Top = child.Depth == 1 ? di.FullName : n.Top;
                            stack.Push(child);
                            continue;
                        }

                        FileInfo fi = (FileInfo)e;
                        string lname = fi.Name.ToLowerInvariant();
                        if (lname == "pagefile.sys" || lname == "hiberfil.sys" || lname == "swapfile.sys") continue;

                        long len = fi.Length;
                        r.TotalBytes += len;
                        r.TotalFiles++;

                        if (n.Top != null)
                        {
                            long cur;
                            if (topBytes.TryGetValue(n.Top, out cur)) topBytes[n.Top] = cur + len;
                            else topBytes[n.Top] = len;
                        }

                        if (watch != null)
                        {
                            watch.Bytes += len;
                            watch.Files++;

                            if ((watch.Id.StartsWith("DL-", StringComparison.Ordinal)) &&
                                len >= InstallerMin && IsInstaller(fi.Name) &&
                                fi.LastWriteTime < DateTime.Now.AddDays(-30))
                            {
                                watch.InstallerBytes += len;
                                watch.InstallerCount++;
                            }
                        }

                        if (deep && len >= TopFileMin)
                        {
                            FileItem f = new FileItem();
                            f.Path = fi.FullName;
                            f.Bytes = len;
                            f.Modified = fi.LastWriteTime;
                            r.TopFiles.Add(f);
                        }

                        if (deep && len >= DupMin && candidates.Count < DupCandidateMax)
                        {
                            FileItem f = new FileItem();
                            f.Path = fi.FullName;
                            f.Bytes = len;
                            f.Modified = fi.LastWriteTime;
                            candidates.Add(f);
                        }
                    }
                    catch { }
                }

                if (progress != null && report.ElapsedMilliseconds >= 200)
                {
                    report.Restart();
                    ScanProgress p = new ScanProgress();
                    p.Files = r.TotalFiles;
                    p.Dirs = r.DirCount;
                    p.Bytes = r.TotalBytes;
                    p.Current = n.Path;
                    progress.Report(p);
                }
            }

            foreach (KeyValuePair<string, long> kv in topBytes)
            {
                DirItem d = new DirItem();
                d.Path = kv.Key;
                d.Bytes = kv.Value;
                d.Depth = 1;
                r.TopDirs.Add(d);
            }
            r.TopDirs.Sort(delegate(DirItem a, DirItem b) { return b.Bytes.CompareTo(a.Bytes); });

            if (deep && !ct.IsCancellationRequested)
            {
                r.TopFiles.Sort(delegate(FileItem a, FileItem b) { return b.Bytes.CompareTo(a.Bytes); });
                if (r.TopFiles.Count > 50) r.TopFiles = r.TopFiles.GetRange(0, 50);

                DetectDuplicates(candidates, r, ct);
                DetectOldLarge(r, candidates);
            }
        }

        private static bool IsInstaller(string name)
        {
            string ext = Path.GetExtension(name).ToLowerInvariant();
            return ext == ".exe" || ext == ".msi" || ext == ".iso" || ext == ".zip" ||
                   ext == ".7z" || ext == ".rar" || ext == ".img";
        }

        // ---------------------------------------------------------- 查重
        private void DetectDuplicates(List<FileItem> files, VolumeScanResult r, CancellationToken ct)
        {
            Dictionary<long, List<FileItem>> bySize = new Dictionary<long, List<FileItem>>();
            foreach (FileItem f in files)
            {
                List<FileItem> list;
                if (!bySize.TryGetValue(f.Bytes, out list)) { list = new List<FileItem>(); bySize[f.Bytes] = list; }
                list.Add(f);
            }

            foreach (KeyValuePair<long, List<FileItem>> kv in bySize)
            {
                if (ct.IsCancellationRequested) return;
                if (kv.Value.Count < 2) continue;

                Dictionary<string, List<FileItem>> byHead = new Dictionary<string, List<FileItem>>();
                foreach (FileItem f in kv.Value)
                {
                    string head = Hash(f.Path, 65536);
                    if (head == null) continue;
                    List<FileItem> list;
                    if (!byHead.TryGetValue(head, out list)) { list = new List<FileItem>(); byHead[head] = list; }
                    list.Add(f);
                }

                foreach (KeyValuePair<string, List<FileItem>> h in byHead)
                {
                    if (h.Value.Count < 2) continue;

                    Dictionary<string, List<FileItem>> byFull = new Dictionary<string, List<FileItem>>();
                    foreach (FileItem f in h.Value)
                    {
                        string full = Hash(f.Path, 0);
                        if (full == null) continue;
                        List<FileItem> list;
                        if (!byFull.TryGetValue(full, out list)) { list = new List<FileItem>(); byFull[full] = list; }
                        list.Add(f);
                    }

                    foreach (KeyValuePair<string, List<FileItem>> g in byFull)
                    {
                        if (g.Value.Count < 2) continue;
                        DupGroup dg = new DupGroup();
                        dg.Size = kv.Key;
                        foreach (FileItem f in g.Value)
                        {
                            if (dg.Files.Count < 20) dg.Files.Add(f.Path);
                            r.DuplicateBytes += f.Bytes;
                        }
                        r.Duplicates.Add(dg);
                    }
                }
            }
        }

        private static string Hash(string path, int limit)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536))
                using (SHA256 sha = SHA256.Create())
                {
                    if (limit <= 0)
                    {
                        return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "");
                    }

                    byte[] buf = new byte[limit];
                    int read = fs.Read(buf, 0, buf.Length);
                    byte[] slice = new byte[read];
                    Array.Copy(buf, slice, read);
                    return BitConverter.ToString(sha.ComputeHash(slice)).Replace("-", "");
                }
            }
            catch { return null; }
        }

        private void DetectOldLarge(VolumeScanResult r, List<FileItem> candidates)
        {
            DateTime cutoff = DateTime.Now.AddYears(-2);
            List<FileItem> old = new List<FileItem>();
            foreach (FileItem f in candidates)
            {
                if (f.Bytes < 1024L * 1024 * 1024) continue;
                if (f.Modified > cutoff) continue;
                old.Add(f);
            }
            old.Sort(delegate(FileItem a, FileItem b) { return b.Bytes.CompareTo(a.Bytes); });
            if (old.Count > 10) old = old.GetRange(0, 10);

            long sum = 0;
            foreach (FileItem f in old) sum += f.Bytes;
            if (old.Count == 0) return;

            VolumeItem item = new VolumeItem();
            item.Id = "OLD-LARGE";
            item.Category = "长期未用";
            item.Label = "超过 2 年未修改的大文件";
            item.Path = "";
            item.Mode = VolumeItemMode.Report;
            item.Deletable = false;
            item.Risk = RiskLevel.High;
            item.Bytes = sum;
            item.Files = old.Count;
            item.Note = "仅统计；可能是备份、镜像或素材，确认前不要删";
            foreach (FileItem f in old) item.Note += "\r\n  " + f.Path;
            r.Items.Add(item);
        }

        // ---------------------------------------------------------- 汇总
        private static void MeasureRecycle(VolumeScanResult r, VolumeInfo v)
        {
            string path = Path.Combine(v.Root, "$Recycle.Bin");
            long bytes;
            long files;
            VolumeUtil.Measure(path, out bytes, out files);
            if (bytes == 0 && files == 0) return;

            VolumeItem item = new VolumeItem();
            item.Id = "RECYCLE";
            item.Category = "回收站";
            item.Label = "回收站";
            item.Path = path;
            item.Mode = VolumeItemMode.Recycle;
            item.Deletable = !v.IsBackup;
            item.Risk = RiskLevel.Low;
            item.Bytes = bytes;
            item.Files = files;
            item.Note = v.IsBackup ? "备份盘：禁止删除" : "清空该盘回收站；其他用户的回收站需要管理员权限";
            r.Items.Add(item);
        }

        private void BuildItems(VolumeScanResult r, VolumeInfo v, List<Watch> watches, bool deep)
        {
            foreach (Watch w in watches)
            {
                if (w.Id == "RECYCLE") continue;
                if (w.Bytes == 0 && w.Files == 0) continue;

                VolumeItem item = new VolumeItem();
                item.Id = w.Id;
                item.Category = w.Category;
                item.Label = w.Label;
                item.Path = w.Root;
                item.Mode = w.Mode;
                item.Deletable = w.Deletable && !v.IsBackup;
                item.Risk = w.Risk;
                item.Bytes = w.Bytes;
                item.Files = w.Files;
                item.Note = w.Note;

                if (w.Id.StartsWith("DL-", StringComparison.Ordinal))
                {
                    item.Bytes = w.InstallerBytes;
                    item.Files = w.InstallerCount;
                    item.Note = w.InstallerCount > 0
                        ? "仅统计：" + w.InstallerCount + " 个安装包/镜像，共 " + Fmt.Size(w.InstallerBytes)
                        : "没有超过 100 MB 且超过 30 天的安装包";
                }
                if (v.IsBackup) item.Note = "备份盘：仅统计，禁止删除";

                r.Items.Add(item);
            }

            if (deep && r.TopFiles.Count > 0)
            {
                long sum = 0;
                foreach (FileItem f in r.TopFiles) sum += f.Bytes;
                VolumeItem item = new VolumeItem();
                item.Id = "TOP-FILES";
                item.Category = "大文件";
                item.Label = "最大的文件";
                item.Mode = VolumeItemMode.Report;
                item.Deletable = false;
                item.Risk = RiskLevel.Medium;
                item.Bytes = sum;
                item.Files = r.TopFiles.Count;
                item.Note = "仅统计；可在右侧排行里查看具体路径";
                r.Items.Add(item);
            }

            if (deep && r.Duplicates.Count > 0)
            {
                VolumeItem item = new VolumeItem();
                item.Id = "DUPES";
                item.Category = "重复文件";
                item.Label = "重复文件组";
                item.Mode = VolumeItemMode.Report;
                item.Deletable = false;
                item.Risk = RiskLevel.High;
                item.Bytes = r.DuplicateBytes;
                item.Files = r.Duplicates.Count;
                item.Note = "仅报告，不提供删除；重复内容可能被程序硬链接，删除前请自行确认";
                r.Items.Add(item);
            }

            r.Items.Sort(delegate(VolumeItem a, VolumeItem b)
            {
                if (a.Deletable != b.Deletable) return a.Deletable ? -1 : 1;
                return b.Bytes.CompareTo(a.Bytes);
            });
        }

        private void BuildRankings(VolumeScanResult r, List<Watch> watches)
        {
            if (r.TopDirs.Count > 20) r.TopDirs = r.TopDirs.GetRange(0, 20);
        }
    }
}
