using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CCleaner.Disks
{
    public class VolumeCleanResult
    {
        public long BeforeFree;
        public long AfterFree;
        public int Errors;
        public List<string> Messages = new List<string>();
        public string LogPath = "";
        public bool Canceled;

        public long Freed
        {
            get { return AfterFree - BeforeFree; }
        }
    }

    public static class VolumeCleaner
    {
        private const uint SHERB_NOCONFIRMATION = 1;
        private const uint SHERB_NOPROGRESSUI = 2;
        private const uint SHERB_NOSOUND = 4;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        public static void Validate(VolumeInfo v, List<VolumeItem> items)
        {
            if (v == null) throw new InvalidOperationException("未选择磁盘");
            if (items == null || items.Count == 0) throw new InvalidOperationException("没有选中任何项目。");

            if (v.IsBackup)
            {
                foreach (VolumeItem item in items)
                {
                    if (item.Mode != VolumeItemMode.Recycle)
                        throw new InvalidOperationException("备份盘只允许清空回收站：" + item.Label);
                }
            }

            foreach (VolumeItem item in items)
            {
                if (!item.Deletable)
                    throw new InvalidOperationException("包含仅报告项，已中止：" + item.Label);
                if (item.Mode == VolumeItemMode.Report)
                    throw new InvalidOperationException("仅报告项不能删除：" + item.Label);
                if (string.IsNullOrEmpty(item.Path))
                    throw new InvalidOperationException("项目缺少路径：" + item.Label);

                if (item.Mode == VolumeItemMode.Recycle)
                {
                    string root = v.Root.TrimEnd('\\');
                    string recycle = Path.Combine(root + "\\", "$Recycle.Bin").TrimEnd('\\');
                    string p = item.Path.TrimEnd('\\');
                    if (!string.Equals(p, root, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(p, recycle, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("回收站项目路径异常：" + item.Path);
                    continue;
                }

                if (!VolumeUtil.IsSafeVolumePath(item.Path, v))
                    throw new InvalidOperationException("路径未通过安全校验：" + item.Path);
            }
        }

        public static VolumeCleanResult Clean(VolumeInfo v, List<VolumeItem> items,
            IProgress<CleanProgress> progress, CancellationToken ct, string outDir)
        {
            Validate(v, items);

            VolumeCleanResult r = new VolumeCleanResult();
            Stopwatch sw = Stopwatch.StartNew();
            r.BeforeFree = new DriveInfo(v.Root).AvailableFreeSpace;

            List<string> log = new List<string>();
            log.Add("磁盘扩展包 清理日志  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            log.Add("磁盘: " + v.Letter + ":  " + v.Label + "  序列号 " + v.Serial + "  " + v.TypeText);
            log.Add("选中: " + items.Count + " 项");
            log.Add("");

            int index = 0;
            foreach (VolumeItem item in items)
            {
                if (ct.IsCancellationRequested) { r.Canceled = true; break; }

                index++;
                Report(progress, index, items.Count, item.Label);

                log.Add("[" + item.Id + "] " + item.Label + "  " + item.Path);
                try
                {
                    int before = r.Errors;
                    if (item.Mode == VolumeItemMode.Recycle)
                    {
                        int hr = SHEmptyRecycleBin(IntPtr.Zero, v.Root,
                            SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                        log.Add("    回收站 HRESULT=" + hr);
                        if (hr < 0)
                        {
                            r.Errors++;
                            r.Messages.Add("回收站清空失败: 0x" + hr.ToString("X8"));
                        }
                    }
                    else
                    {
                        List<string> errs = new List<string>();
                        int removed = DeleteContents(item.Path, errs, ct);
                        log.Add("    删除条目 " + removed + " 个，错误 " + errs.Count);
                        foreach (string e in errs)
                        {
                            r.Errors++;
                            if (r.Messages.Count < 100) r.Messages.Add(e);
                            log.Add("    " + e);
                        }
                        if (r.Errors == before) log.Add("    完成");
                    }
                }
                catch (Exception ex)
                {
                    r.Errors++;
                    if (r.Messages.Count < 100) r.Messages.Add(item.Label + ": " + ex.Message);
                    log.Add("    失败: " + ex.Message);
                }

                log.Add("    用时 " + Fmt.Duration(sw.Elapsed));
            }

            r.AfterFree = new DriveInfo(v.Root).AvailableFreeSpace;
            log.Add("");
            log.Add(string.Format("可用空间: {0} -> {1}，实际释放 {2}",
                Fmt.Size(r.BeforeFree), Fmt.Size(r.AfterFree), Fmt.Size(r.Freed)));
            log.Add("未删除条目: " + r.Errors);
            log.Add("总用时: " + Fmt.Duration(sw.Elapsed));

            try
            {
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                r.LogPath = Path.Combine(outDir, "Disk-Clean-Log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
                File.WriteAllLines(r.LogPath, log.ToArray(), new UTF8Encoding(true));
            }
            catch { }

            return r;
        }

        private static void Report(IProgress<CleanProgress> progress, int index, int total, string msg)
        {
            if (progress == null) return;
            CleanProgress p = new CleanProgress();
            p.Index = index;
            p.Total = total;
            p.Message = msg;
            progress.Report(p);
        }

        private static int DeleteContents(string dir, List<string> errs, CancellationToken ct)
        {
            if (!Directory.Exists(dir)) return 0;
            FileSystemInfo[] children;
            try { children = new DirectoryInfo(dir).GetFileSystemInfos(); }
            catch (Exception ex) { errs.Add(dir + " -> " + ex.Message); return 0; }

            int n = 0;
            foreach (FileSystemInfo c in children)
            {
                if (ct.IsCancellationRequested) break;
                if (DeletePath(c.FullName, errs, ct)) n++;
            }
            return n;
        }

        private static bool DeletePath(string path, List<string> errs, CancellationToken ct)
        {
            try
            {
                if (!Directory.Exists(path) && !File.Exists(path)) return false;

                if (CCleaner.Util.IsReparse(path))
                {
                    Directory.Delete(path, false);
                    return true;
                }

                FileAttributes attr = File.GetAttributes(path);
                if ((attr & FileAttributes.Directory) != 0)
                {
                    try { Directory.Delete(path, true); return true; }
                    catch { return DeleteTreeRobust(path, errs, ct); }
                }

                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                errs.Add(path + " -> " + ex.Message);
                return false;
            }
        }

        private static bool DeleteTreeRobust(string root, List<string> errs, CancellationToken ct)
        {
            Stack<string> stack = new Stack<string>();
            List<string> dirs = new List<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                if (ct.IsCancellationRequested) break;
                string p = stack.Pop();
                FileSystemInfo[] entries;
                try { entries = new DirectoryInfo(p).GetFileSystemInfos(); }
                catch (Exception ex) { errs.Add(p + " -> " + ex.Message); continue; }

                dirs.Add(p);
                foreach (FileSystemInfo e in entries)
                {
                    try
                    {
                        if (((int)e.Attributes & 1024) != 0)
                        {
                            Directory.Delete(e.FullName, false);
                            continue;
                        }
                        DirectoryInfo di = e as DirectoryInfo;
                        if (di != null) stack.Push(di.FullName);
                        else
                        {
                            File.SetAttributes(e.FullName, FileAttributes.Normal);
                            File.Delete(e.FullName);
                        }
                    }
                    catch (Exception ex) { errs.Add(e.FullName + " -> " + ex.Message); }
                }
            }

            for (int i = dirs.Count - 1; i >= 0; i--)
            {
                try { Directory.Delete(dirs[i], false); }
                catch (Exception ex) { errs.Add(dirs[i] + " -> " + ex.Message); }
            }
            return true;
        }
    }
}
