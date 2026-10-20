using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CCleaner
{
    public class Scanner
    {
        private const long TopFileMin = 50L * 1024 * 1024;

        private readonly string _userProfile;
        private readonly string _local;
        private readonly string _roaming;

        private List<CleanTarget> _targets;
        private Dictionary<string, CleanTarget> _byPath;
        private List<string> _denied;
        private long _deniedCount;
        private Dictionary<string, long> _dirTotal;
        private Dictionary<string, int> _dirDepth;
        private List<FileItem> _topFiles;
        private long _totalBytes;
        private long _totalFiles;
        private long _dirCount;
        private HashSet<string> _skipRoots;

        private class Node
        {
            public string Path;
            public int Depth;
            public CleanTarget Target;
            public string Anc4;

            public Node(string p, int d, CleanTarget t, string a)
            {
                Path = p;
                Depth = d;
                Target = t;
                Anc4 = a;
            }
        }

        public Scanner(string userProfile, string local, string roaming)
        {
            _userProfile = userProfile;
            _local = local;
            _roaming = roaming;
        }

        public List<CleanTarget> Targets
        {
            get { return _targets; }
        }

        public ScanResult Scan(IProgress<ScanProgress> progress, CancellationToken ct)
        {
            Stopwatch sw = Stopwatch.StartNew();

            _targets = TargetCatalog.Create(_userProfile, _local, _roaming);
            _byPath = new Dictionary<string, CleanTarget>(StringComparer.OrdinalIgnoreCase);
            _denied = new List<string>();
            _deniedCount = 0;
            _dirTotal = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            _dirDepth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _topFiles = new List<FileItem>();
            _totalBytes = 0;
            _totalFiles = 0;
            _dirCount = 0;
            _skipRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _skipRoots.Add(@"C:\System Volume Information");

            ResolveTargets();
            Walk(progress, ct);
            Aggregate();
            MarkDeniedTargets();

            ScanResult r = new ScanResult();
            r.GeneratedAt = DateTime.Now;
            r.Elevated = Util.IsAdmin();
            r.DriveTotal = Util.TotalSpace("C");
            r.DriveFree = Util.FreeSpace("C");
            r.DriveUsed = r.DriveTotal - r.DriveFree;
            r.TotalBytes = _totalBytes;
            r.TotalFiles = _totalFiles;
            r.DirCount = _dirCount;
            r.DeniedCount = _deniedCount;
            r.DeniedPaths = new List<string>(_denied);
            r.Targets = _targets;

            long winsxs = 0;
            _dirTotal.TryGetValue(@"C:\Windows\WinSxS", out winsxs);
            r.WinSxSBytes = winsxs;

            foreach (CleanTarget t in _targets)
            {
                if (t.Id == "COMP-01" && winsxs > 0)
                {
                    t.Note = "目录实测 " + Fmt.Size(winsxs) + "（含硬链接，实际占用更小）；DISM 清理通常释放 1-4 GB，耗时 5-15 分钟";
                }
            }

            BuildCategories(r);
            BuildRankings(r);
            r.Suggestions = BuildSuggestions(r);
            r.DurationSec = sw.Elapsed.TotalSeconds;

            if (progress != null)
            {
                ScanProgress sp = new ScanProgress();
                sp.Files = _totalFiles;
                sp.Dirs = _dirCount;
                sp.Bytes = _totalBytes;
                sp.Current = "完成";
                progress.Report(sp);
            }
            return r;
        }

        // ---------------- 目标解析 ----------------
        private void ResolveTargets()
        {
            foreach (CleanTarget t in _targets)
            {
                if (t.Mode == TargetMode.Dism || t.Mode == TargetMode.Shadow)
                {
                    t.Exists = true;
                    continue;
                }

                List<string> found = Expand(t.PathPattern);
                List<string> paths = new List<string>();

                foreach (string f in found)
                {
                    if (string.IsNullOrEmpty(f)) continue;
                    if (Util.IsReparse(f)) continue;

                    if (File.Exists(Util.LP(f)))
                    {
                        try
                        {
                            FileInfo fi = new FileInfo(Util.LP(f));
                            t.Bytes += fi.Length;
                            t.Files++;
                            paths.Add(f);
                        }
                        catch { }
                        continue;
                    }

                    if (!Directory.Exists(Util.LP(f))) continue;
                    if (_byPath.ContainsKey(f)) continue;
                    _byPath[f] = t;
                    paths.Add(f);
                    try { t.LastWrite = Directory.GetLastWriteTime(Util.LP(f)); } catch { }
                }

                List<string> mine = new List<string>();
                List<string> rest = new List<string>();
                string prefix = _userProfile + "\\";
                foreach (string p in paths)
                {
                    if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) mine.Add(p);
                    else rest.Add(p);
                }
                mine.AddRange(rest);
                t.Paths = mine;
                t.Exists = mine.Count > 0;
            }
        }

        private List<string> Expand(string pattern)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(pattern)) return result;

            if (pattern.IndexOfAny(new char[] { '*', '?' }) < 0)
            {
                if (Directory.Exists(Util.LP(pattern)) || File.Exists(Util.LP(pattern))) result.Add(Util.NP(pattern));
                return result;
            }

            string[] parts = pattern.Split('\\');
            List<string> current = new List<string>();
            current.Add(parts[0] + "\\");

            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length == 0) continue;
                List<string> next = new List<string>();
                bool last = (i == parts.Length - 1);

                foreach (string c in current)
                {
                    if (part.IndexOfAny(new char[] { '*', '?' }) >= 0)
                    {
                        try
                        {
                            foreach (string d in Directory.GetDirectories(Util.LP(c), part)) next.Add(Util.NP(d));
                            if (last)
                            {
                                foreach (string f in Directory.GetFiles(Util.LP(c), part)) next.Add(Util.NP(f));
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        string p = Path.Combine(c, part);
                        if (Directory.Exists(Util.LP(p)) || File.Exists(Util.LP(p))) next.Add(p);
                    }
                }

                current = next;
                if (current.Count == 0) break;
            }

            result.AddRange(current);
            return result;
        }

        // ---------------- 遍历 ----------------
        private void Walk(IProgress<ScanProgress> progress, CancellationToken ct)
        {
            Stack<Node> stack = new Stack<Node>();
            stack.Push(new Node(@"C:\", 0, null, ""));
            Stopwatch reportSw = Stopwatch.StartNew();

            while (stack.Count > 0)
            {
                if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);

                Node node = stack.Pop();
                if (_skipRoots.Contains(node.Path)) continue;

                CleanTarget tid = node.Target;
                CleanTarget mapped;
                if (_byPath.TryGetValue(node.Path, out mapped)) tid = mapped;

                FileSystemInfo[] entries;
                try
                {
                    entries = new DirectoryInfo(Util.LP(node.Path)).GetFileSystemInfos();
                }
                catch
                {
                    AddDenied(node.Path);
                    continue;
                }

                _dirCount++;
                long own = 0;

                for (int i = 0; i < entries.Length; i++)
                {
                    FileSystemInfo e = entries[i];
                    if (((int)e.Attributes & 1024) != 0) continue;   // ReparsePoint

                    DirectoryInfo di = e as DirectoryInfo;
                    if (di != null)
                    {
                        string fp = Util.NP(di.FullName);
                        int cd = node.Depth + 1;
                        string ca = node.Anc4;
                        if (cd <= 4) ca = fp;
                        stack.Push(new Node(fp, cd, tid, ca));
                    }
                    else
                    {
                        FileInfo fi = (FileInfo)e;
                        string n = fi.Name;
                        if (n == "pagefile.sys" || n == "hiberfil.sys" || n == "swapfile.sys") continue;

                        long len = fi.Length;
                        own += len;
                        _totalFiles++;

                        if (len >= TopFileMin)
                        {
                            FileItem it = new FileItem();
                            it.Path = Util.NP(fi.FullName);
                            it.Bytes = len;
                            it.Modified = fi.LastWriteTime;
                            _topFiles.Add(it);
                        }
                        if (tid != null)
                        {
                            tid.Bytes += len;
                            tid.Files++;
                        }
                    }
                }

                _totalBytes += own;

                if (!string.IsNullOrEmpty(node.Anc4))
                {
                    long cur;
                    if (_dirTotal.TryGetValue(node.Anc4, out cur)) _dirTotal[node.Anc4] = cur + own;
                    else
                    {
                        _dirTotal[node.Anc4] = own;
                        _dirDepth[node.Anc4] = Math.Min(node.Depth, 4);
                    }
                }

                if (progress != null && reportSw.ElapsedMilliseconds >= 200)
                {
                    reportSw.Restart();
                    ScanProgress sp = new ScanProgress();
                    sp.Files = _totalFiles;
                    sp.Dirs = _dirCount;
                    sp.Bytes = _totalBytes;
                    sp.Current = node.Path;
                    progress.Report(sp);
                }
            }
        }

        private void AddDenied(string path)
        {
            _deniedCount++;
            if (_denied.Count < 5000) _denied.Add(path);
        }

        private void Aggregate()
        {
            Dictionary<int, List<string>> buckets = new Dictionary<int, List<string>>();
            for (int i = 1; i <= 4; i++) buckets[i] = new List<string>();

            foreach (string k in _dirTotal.Keys)
            {
                int d;
                if (!_dirDepth.TryGetValue(k, out d)) d = 4;
                if (d >= 1 && d <= 4) buckets[d].Add(k);
            }

            for (int d = 4; d >= 2; d--)
            {
                foreach (string k in buckets[d])
                {
                    int idx = k.LastIndexOf('\\');
                    if (idx <= 2) continue;
                    string parent = k.Substring(0, idx);
                    long pv;
                    if (_dirTotal.TryGetValue(parent, out pv)) _dirTotal[parent] = pv + _dirTotal[k];
                }
            }
        }

        private void MarkDeniedTargets()
        {
            foreach (CleanTarget t in _targets)
            {
                if (t.Denied || t.Bytes > 0) continue;
                if (t.Mode == TargetMode.Dism || t.Mode == TargetMode.Shadow) continue;
                foreach (string p in t.Paths)
                {
                    bool hit = false;
                    foreach (string d in _denied)
                    {
                        if (string.Equals(d, p, StringComparison.OrdinalIgnoreCase) ||
                            d.StartsWith(p + "\\", StringComparison.OrdinalIgnoreCase) ||
                            p.StartsWith(d + "\\", StringComparison.OrdinalIgnoreCase))
                        {
                            hit = true;
                            break;
                        }
                    }
                    if (hit) { t.Denied = true; break; }
                }
            }
        }

        // ---------------- 汇总 ----------------
        private void BuildCategories(ScanResult r)
        {
            List<CategoryDef> defs = TargetCatalog.CreateCategories();
            List<CategoryResult> cats = new List<CategoryResult>();

            foreach (CategoryDef def in defs)
            {
                CategoryResult cr = new CategoryResult();
                cr.Def = def;
                foreach (CleanTarget t in _targets)
                    if (t.Cat == def.Id && t.Exists) cr.Items.Add(t);

                cr.Items.Sort(delegate(CleanTarget a, CleanTarget b)
                {
                    if (a.Deletable != b.Deletable) return a.Deletable ? -1 : 1;
                    return b.Bytes.CompareTo(a.Bytes);
                });

                foreach (CleanTarget t in cr.Items)
                {
                    if (t.Deletable)
                    {
                        cr.CleanBytes += t.Bytes;
                        if (t.Recommend) cr.RecBytes += t.Bytes;
                    }
                    else cr.ReportBytes += t.Bytes;
                }

                r.CleanBytes += cr.CleanBytes;
                r.RecBytes += cr.RecBytes;
                r.ReportBytes += cr.ReportBytes;
                cats.Add(cr);
            }
            r.Categories = cats;
        }

        private void BuildRankings(ScanResult r)
        {
            List<DirItem> dirs = new List<DirItem>();
            foreach (string k in _dirTotal.Keys)
            {
                int d;
                if (!_dirDepth.TryGetValue(k, out d)) d = 4;
                DirItem it = new DirItem();
                it.Path = k;
                it.Bytes = _dirTotal[k];
                it.Depth = d;
                dirs.Add(it);
            }
            dirs.Sort(delegate(DirItem a, DirItem b) { return b.Bytes.CompareTo(a.Bytes); });
            if (dirs.Count > 30) dirs = dirs.GetRange(0, 30);
            r.TopDirs = dirs;

            _topFiles.Sort(delegate(FileItem a, FileItem b) { return b.Bytes.CompareTo(a.Bytes); });
            if (_topFiles.Count > 100) _topFiles = _topFiles.GetRange(0, 100);
            r.TopFiles = _topFiles;
        }

        private static CleanTarget Find(List<CleanTarget> list, string id)
        {
            foreach (CleanTarget t in list) if (t.Id == id) return t;
            return null;
        }

        private List<Suggestion> BuildSuggestions(ScanResult r)
        {
            List<Suggestion> s = new List<Suggestion>();
            double freePct = r.DriveTotal > 0 ? r.DriveFree * 100.0 / r.DriveTotal : 0;

            if (freePct < 15)
            {
                s.Add(S("可用空间告急", string.Format("C 盘可用率只有 {0:0.0}%，低于 15% 警戒线。Windows 更新、临时文件写入都可能失败，建议优先释放 20 GB 以上。", freePct), 0));
            }

            CleanTarget steam = Find(_targets, "GAME-04");
            if (steam != null && steam.Bytes > 10L * 1024 * 1024 * 1024)
            {
                s.Add(S("把 Steam 游戏搬到 D 盘", string.Format("Steam 游戏库本体占用 {0}。打开 Steam -> 设置 -> 存储，选中安装文件夹后点「移动」，把它迁到 D 盘。这是释放 C 盘最快且无损的办法，不需要删除游戏。", Fmt.Size(steam.Bytes)), steam.Bytes));
            }

            CleanTarget xbox = Find(_targets, "GAME-05");
            if (xbox != null && xbox.Bytes > 5L * 1024 * 1024 * 1024)
            {
                s.Add(S("把 Xbox 游戏迁移到 D 盘", string.Format("Xbox 游戏占用 {0}。打开 设置 -> 应用 -> 已安装的应用，选中游戏 -> 移动 -> 选择 D 盘。UWP 游戏不能直接剪切，必须走系统迁移。", Fmt.Size(xbox.Bytes)), xbox.Bytes));
            }

            if (r.WinSxSBytes > 5L * 1024 * 1024 * 1024)
            {
                s.Add(S("清理 WinSxS 组件存储", string.Format("WinSxS 目录实测 {0}（含硬链接，实际磁盘占用通常更小）。DISM 组件清理一般能释放 1-4 GB，耗时 5-15 分钟，清理后无法卸载旧更新。", Fmt.Size(r.WinSxSBytes)), Math.Min(r.WinSxSBytes, 4L * 1024 * 1024 * 1024)));
            }

            CleanTarget nv = Find(_targets, "GAME-08");
            if (nv != null && nv.Bytes > 5L * 1024 * 1024 * 1024)
            {
                s.Add(S("NVIDIA 着色器缓存异常膨胀", string.Format("NVIDIA 的 DirectX 着色器缓存占了 {0}（正常通常只有几百 MB）。这是驱动缓存不回收的已知问题，纯缓存，删除后游戏首次启动会重新编译着色器，之后恢复正常。", Fmt.Size(nv.Bytes)), nv.Bytes));
            }

            long devBytes = 0;
            foreach (CleanTarget t in _targets) if (t.Cat == "dev" && t.Deletable) devBytes += t.Bytes;
            if (devBytes > 1024L * 1024 * 1024)
            {
                s.Add(S("开发缓存可整体重建", string.Format("npm / pnpm / pip / NuGet / Gradle 等缓存合计 {0}。删掉后首次安装依赖会重新下载，但项目本身不受影响。", Fmt.Size(devBytes)), devBytes));
            }

            CleanTarget dl = Find(_targets, "USER-01");
            if (dl != null && dl.Bytes > 2L * 1024 * 1024 * 1024)
            {
                s.Add(S("下载目录里多半是旧安装包", string.Format("下载文件夹占用 {0}。按修改时间排序，把半年没用过的 .exe / .msi / .zip 删掉或移到 D 盘，通常能腾出相当可观的空间。", Fmt.Size(dl.Bytes)), dl.Bytes));
            }

            long chatBytes = 0;
            foreach (string id in new string[] { "APP-01", "APP-02", "APP-03", "APP-04" })
            {
                CleanTarget t = Find(_targets, id);
                if (t != null) chatBytes += t.Bytes;
            }
            if (chatBytes > 5L * 1024 * 1024 * 1024)
            {
                s.Add(S("聊天软件的数据不要直接删", string.Format("微信 / QQ / 钉钉的数据合计 {0}。请在各软件的「设置 -> 文件管理」里把存储位置改到 D 盘，或使用软件自带的清理功能，直接删除会丢失聊天记录和收到的文件。", Fmt.Size(chatBytes)), 0));
            }

            CleanTarget recycle = Find(_targets, "SYS-12");
            if (recycle != null && recycle.Bytes > 500L * 1024 * 1024)
            {
                s.Add(S("回收站占用偏高", string.Format("C 盘回收站里有 {0}。确认没有需要恢复的文件后清空即可。", Fmt.Size(recycle.Bytes)), recycle.Bytes));
            }

            int bigCount = 0;
            List<string> bigNames = new List<string>();
            foreach (FileItem f in r.TopFiles)
            {
                if (f.Bytes > 2L * 1024 * 1024 * 1024)
                {
                    bigNames.Add(Path.GetFileName(f.Path) + " (" + Fmt.Size(f.Bytes) + ")");
                    bigCount++;
                    if (bigCount >= 3) break;
                }
            }
            if (bigNames.Count > 0)
            {
                s.Add(S("存在单个超大文件", "检测到超过 2 GB 的单文件：" + string.Join("、", bigNames.ToArray()) + "。如果是虚拟机磁盘、镜像或视频素材，建议直接移到 D 盘。", 0));
            }

            if (r.CleanBytes > 5L * 1024 * 1024 * 1024)
            {
                s.Add(S("本次可清理总量", string.Format("勾选项目合计 {0}，其中推荐项约 {1}。建议先执行推荐项，观察一周无异常后再处理低优先级项目。", Fmt.Size(r.CleanBytes), Fmt.Size(r.RecBytes)), r.CleanBytes));
            }

            return s;
        }

        private static Suggestion S(string title, string body, long bytes)
        {
            Suggestion x = new Suggestion();
            x.Title = title;
            x.Body = body;
            x.Bytes = bytes;
            return x;
        }
    }
}
