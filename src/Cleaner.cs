using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CCleaner
{
    public class Cleaner
    {
        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        private readonly string _outDir;
        private readonly string _userProfile;

        public Cleaner(string outDir, string userProfile)
        {
            _outDir = outDir;
            _userProfile = userProfile;
        }

        public string OutDir
        {
            get { return _outDir; }
        }

        public CleanResult Clean(List<CleanTarget> items, bool dryRun, IProgress<CleanProgress> progress, CancellationToken ct)
        {
            Validate(items);

            CleanResult result = new CleanResult();
            result.DryRun = dryRun;
            result.BeforeFree = Util.FreeSpace("C");

            List<string> log = new List<string>();
            log.Add("C盘清理助手 清理日志  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            log.Add("模式: " + (dryRun ? "演练（不删除）" : "永久删除"));
            log.Add("选中: " + items.Count + " 项");
            log.Add("");

            EnsureOutDir();
            string logPath = Path.Combine(_outDir, "C-Clean-Log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
            result.LogPath = logPath;

            int index = 0;
            foreach (CleanTarget item in items)
            {
                if (ct.IsCancellationRequested)
                {
                    result.Canceled = true;
                    log.Add("已取消（剩余项目未执行）");
                    break;
                }

                index++;
                Report(progress, index, items.Count, item.Id, item.Label);

                bool itemFailed = false;
                switch (item.Mode)
                {
                    case TargetMode.Dism:
                        if (!dryRun) itemFailed = !RunDism(log, ct);
                        else log.Add("[COMP] 演练：将执行 DISM 组件清理");
                        break;

                    case TargetMode.Shadow:
                        if (!dryRun) itemFailed = !DeleteShadowCopies(log, result);
                        else log.Add("[COMP] 演练：将删除旧还原点");
                        break;

                    case TargetMode.Recycle:
                        if (!dryRun) itemFailed = !EmptyRecycle(log, result);
                        else log.Add("[SYS] 演练：将清空 C 盘回收站");
                        break;

                    case TargetMode.Contents:
                        if (dryRun)
                        {
                            log.Add("[" + item.Id + "] 演练: " + item.Label + " (" + Fmt.Size(item.Bytes) + ")");
                            break;
                        }
                        log.Add("[" + item.Id + "] " + item.Label);
                        foreach (string p in item.Paths)
                        {
                            if (ct.IsCancellationRequested) { result.Canceled = true; break; }
                            int before = result.Errors;
                            DeleteContents(p, result, log, ct);
                            if (result.Errors > before) itemFailed = true;
                        }
                        break;

                    case TargetMode.Path:
                        if (dryRun)
                        {
                            log.Add("[" + item.Id + "] 演练: " + item.Label + " (" + Fmt.Size(item.Bytes) + ")");
                            break;
                        }
                        log.Add("[" + item.Id + "] " + item.Label);
                        foreach (string p in item.Paths)
                        {
                            if (ct.IsCancellationRequested) { result.Canceled = true; break; }
                            int before = result.Errors;
                            DeletePath(p, result, log, ct);
                            if (result.Errors > before) itemFailed = true;
                        }
                        break;

                    default:
                        log.Add("[" + item.Id + "] 跳过（仅统计项）");
                        break;
                }

                if (itemFailed) log.Add("    [!] 存在未能删除的条目（多为占用中）");
                if (result.Canceled) break;
            }

            result.AfterFree = Util.FreeSpace("C");

            log.Add("");
            log.Add(string.Format("可用空间: {0} -> {1}，实际释放 {2}", Fmt.Size(result.BeforeFree), Fmt.Size(result.AfterFree), Fmt.Size(result.Freed)));
            log.Add(string.Format("未删除条目: {0}", result.Errors));

            try
            {
                EnsureOutDir();
                File.WriteAllLines(logPath, log.ToArray(), new UTF8Encoding(true));
            }
            catch { }

            return result;
        }

        /// <summary>删除前逐条校验路径，任意一条不合法则整体中止。</summary>
        public static void Validate(List<CleanTarget> items)
        {
            List<string> bad = new List<string>();
            foreach (CleanTarget item in items)
            {
                if (item.Mode == TargetMode.Dism || item.Mode == TargetMode.Shadow || item.Mode == TargetMode.Recycle) continue;
                if (!item.Deletable) continue;
                foreach (string p in item.Paths)
                {
                    if (!Util.IsSafeDeletePath(p)) bad.Add(item.Id + "  " + p);
                }
            }
            if (bad.Count > 0)
            {
                throw new InvalidOperationException("以下路径未通过安全校验，已中止：\r\n" + string.Join("\r\n", bad.ToArray()));
            }
        }

        /// <summary>管理员复测扫描阶段读不到的项目。</summary>
        public static void RefreshDeniedSizes(List<CleanTarget> items, IProgress<CleanProgress> progress)
        {
            int i = 0;
            foreach (CleanTarget item in items)
            {
                i++;
                if (!item.Denied) continue;
                if (item.Mode != TargetMode.Contents && item.Mode != TargetMode.Path) continue;
                long tb = 0;
                long tf = 0;
                foreach (string p in item.Paths)
                {
                    if (!Directory.Exists(Util.LP(p)) && !File.Exists(Util.LP(p))) continue;
                    long b;
                    long f;
                    Measure(p, out b, out f);
                    tb += b;
                    tf += f;
                }
                if (tb > 0)
                {
                    item.Bytes = tb;
                    item.Files = tf;
                    item.Denied = false;
                }
                Report(progress, i, items.Count, item.Id, item.Label);
            }
        }

        private static void Measure(string path, out long bytes, out long files)
        {
            bytes = 0;
            files = 0;
            Stack<string> stack = new Stack<string>();
            stack.Push(path);
            while (stack.Count > 0)
            {
                string p = stack.Pop();
                FileSystemInfo[] entries;
                try { entries = new DirectoryInfo(Util.LP(p)).GetFileSystemInfos(); }
                catch { continue; }
                foreach (FileSystemInfo e in entries)
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
            }
        }

        private void EnsureOutDir()
        {
            if (!Directory.Exists(_outDir)) Directory.CreateDirectory(_outDir);
        }

        private static void Report(IProgress<CleanProgress> progress, int index, int total, string id, string msg)
        {
            if (progress == null) return;
            CleanProgress p = new CleanProgress();
            p.Index = index;
            p.Total = total;
            p.ItemId = id;
            p.Message = msg;
            progress.Report(p);
        }

        private void AddError(CleanResult result, string message)
        {
            result.Errors++;
            if (result.ErrorMessages.Count < 200) result.ErrorMessages.Add(message);
        }

        // ---------------- 删除实现 ----------------
        private void DeleteContents(string dir, CleanResult result, List<string> log, CancellationToken ct)
        {
            if (!Directory.Exists(Util.LP(dir)))
            {
                log.Add("    已不存在: " + dir);
                return;
            }
            FileSystemInfo[] children;
            try { children = new DirectoryInfo(Util.LP(dir)).GetFileSystemInfos(); }
            catch (Exception ex)
            {
                AddError(result, dir + " -> " + ex.Message);
                log.Add("    无法枚举: " + dir + " -> " + ex.Message);
                return;
            }

            log.Add("    清理内容: " + dir);
            foreach (FileSystemInfo c in children)
            {
                if (ct.IsCancellationRequested) return;
                DeletePath(c.FullName, result, log, ct);
            }
        }

        private void DeletePath(string path, CleanResult result, List<string> log, CancellationToken ct)
        {
            try
            {
                if (!Directory.Exists(Util.LP(path)) && !File.Exists(Util.LP(path))) return;

                if (Util.IsReparse(path))
                {
                    Directory.Delete(Util.LP(path), false);
                    return;
                }

                FileAttributes attr = File.GetAttributes(Util.LP(path));
                if ((attr & FileAttributes.Directory) != 0)
                {
                    try
                    {
                        Directory.Delete(Util.LP(path), true);
                        return;
                    }
                    catch
                    {
                        DeleteTreeRobust(path, result, ct);
                        return;
                    }
                }

                File.SetAttributes(Util.LP(path), FileAttributes.Normal);
                File.Delete(Util.LP(path));
            }
            catch (Exception ex)
            {
                AddError(result, path + " -> " + ex.Message);
            }
        }

        private void DeleteTreeRobust(string root, CleanResult result, CancellationToken ct)
        {
            Stack<string> stack = new Stack<string>();
            List<string> dirs = new List<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                if (ct.IsCancellationRequested) return;
                string p = stack.Pop();
                FileSystemInfo[] entries;
                try { entries = new DirectoryInfo(Util.LP(p)).GetFileSystemInfos(); }
                catch (Exception ex)
                {
                    AddError(result, p + " -> " + ex.Message);
                    continue;
                }

                dirs.Add(p);
                foreach (FileSystemInfo e in entries)
                {
                    try
                    {
                        if (((int)e.Attributes & 1024) != 0)
                        {
                            Directory.Delete(Util.LP(e.FullName), false);
                            continue;
                        }
                        DirectoryInfo di = e as DirectoryInfo;
                        if (di != null) stack.Push(di.FullName);
                        else
                        {
                            File.SetAttributes(Util.LP(e.FullName), FileAttributes.Normal);
                            File.Delete(Util.LP(e.FullName));
                        }
                    }
                    catch (Exception ex)
                    {
                        AddError(result, e.FullName + " -> " + ex.Message);
                    }
                }
            }

            for (int i = dirs.Count - 1; i >= 0; i--)
            {
                try { Directory.Delete(Util.LP(dirs[i]), false); }
                catch (Exception ex) { AddError(result, dirs[i] + " -> " + ex.Message); }
            }
        }

        // ---------------- DISM ----------------
        private bool RunDism(List<string> log, CancellationToken ct)
        {
            string exe = Path.Combine(Environment.SystemDirectory, "Dism.exe");
            if (!File.Exists(exe))
            {
                log.Add("    找不到 Dism.exe");
                return false;
            }

            log.Add("    执行 DISM 组件清理，可能需要 5-15 分钟...");
            StringBuilder sb = new StringBuilder();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, "/Online /Cleanup-Image /StartComponentCleanup");
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) lock (sb) { sb.AppendLine(e.Data); }
                    };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) lock (sb) { sb.AppendLine(e.Data); }
                    };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    while (!p.HasExited)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            try { p.Kill(); } catch { }
                            break;
                        }
                        Thread.Sleep(200);
                    }
                    p.WaitForExit();
                    int code = p.ExitCode;
                    log.Add("    DISM 退出码 " + code);
                    if (code != 0 && code != 3010)
                    {
                        log.Add("    DISM 输出: " + sb.ToString().Trim());
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                log.Add("    DISM 执行失败: " + ex.Message);
                return false;
            }
        }

        // ---------------- 还原点 ----------------
        private bool DeleteShadowCopies(List<string> log, CleanResult result)
        {
            try
            {
                List<ManagementObject> shadows = new List<ManagementObject>();
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ShadowCopy"))
                {
                    foreach (ManagementBaseObject o in searcher.Get()) shadows.Add((ManagementObject)o);
                }

                log.Add("    找到 " + shadows.Count + " 个还原点");
                if (shadows.Count <= 1)
                {
                    log.Add("    少于等于 1 个，不删除");
                    return true;
                }

                shadows.Sort(delegate(ManagementObject a, ManagementObject b)
                {
                    string da = Convert.ToString(a["InstallDate"]);
                    string db = Convert.ToString(b["InstallDate"]);
                    return string.Compare(db, da, StringComparison.Ordinal);
                });

                int deleted = 0;
                bool failed = false;
                for (int i = 1; i < shadows.Count; i++)
                {
                    try
                    {
                        shadows[i].InvokeMethod("Delete", null);
                        deleted++;
                    }
                    catch (Exception ex)
                    {
                        failed = true;
                        AddError(result, "还原点删除失败: " + ex.Message);
                    }
                }
                log.Add("    已删除 " + deleted + " 个旧还原点，保留最新 1 个");
                return !failed;
            }
            catch (Exception ex)
            {
                AddError(result, "还原点操作失败: " + ex.Message);
                log.Add("    还原点操作失败: " + ex.Message);
                return false;
            }
        }

        // ---------------- 回收站 ----------------
        private bool EmptyRecycle(List<string> log, CleanResult result)
        {
            try
            {
                int hr = SHEmptyRecycleBin(IntPtr.Zero, "C:\\", SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                log.Add("    回收站清空 HRESULT=" + hr);
                if (hr < 0)
                {
                    AddError(result, "回收站清空失败: 0x" + hr.ToString("X8"));
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                AddError(result, "回收站清空失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>供界面展示：计算目录大小。</summary>
        public static long MeasureDirectory(string path)
        {
            long b;
            long f;
            Measure(path, out b, out f);
            return b;
        }
    }
}
