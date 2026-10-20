using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CCleaner.Setup
{
    public static class Installer
    {
        public const string AppName = "C盘清理助手";
        public static readonly string Version = VersionInfo.Version;
        public const string Publisher = "Misaka-11736";
        public const string RegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CDriveCleaner";
        private const string PayloadName = "CCleanerSetup.payload.exe";

        private const uint MOVEFILE_DELAY_UNTIL_REBOOT = 0x4;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string existingFileName, string newFileName, uint flags);

        public static string DefaultDir
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", AppName);
            }
        }

        public static string AppExePath(string dir)
        {
            return Path.Combine(dir, AppName + ".exe");
        }

        public static string InstalledDir()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegKey))
                {
                    if (k != null)
                    {
                        string d = Convert.ToString(k.GetValue("InstallLocation"));
                        if (!string.IsNullOrEmpty(d)) return d;
                    }
                }
            }
            catch { }
            return DefaultDir;
        }

        public static bool IsInstalled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegKey))
                    if (k != null) return true;
            }
            catch { }
            return false;
        }

        public static void Install(string dir, bool desktop, bool launch, bool silent)
        {
            if (string.IsNullOrWhiteSpace(dir)) throw new ArgumentException("安装目录不能为空。");
            if (!EnsureAppClosed(silent)) throw new OperationCanceledException("程序正在运行，安装已取消。");

            Directory.CreateDirectory(dir);

            string exe = AppExePath(dir);
            string uninstall = Path.Combine(dir, "uninstall.exe");

            ExtractPayload(exe);
            File.Copy(Application.ExecutablePath, uninstall, true);

            CreateShortcut(StartMenuShortcut(), exe, dir);
            CreateShortcut(UninstallShortcut(), uninstall, dir);
            if (desktop) CreateShortcut(DesktopShortcut(), exe, dir);
            else TryDelete(DesktopShortcut());

            WriteUninstallEntry(dir, exe, uninstall);

            if (launch)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(exe);
                    psi.WorkingDirectory = dir;
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
                catch { }
            }
        }

        public static void Uninstall(bool silent)
        {
            string dir = InstalledDir();
            if (!EnsureAppClosed(silent)) throw new OperationCanceledException("程序正在运行，卸载已取消。");

            TryDelete(StartMenuShortcut());
            TryDelete(UninstallShortcut());
            TryDelete(DesktopShortcut());
            RemoveUninstallEntry();

            if (!Directory.Exists(dir)) return;

            string temp = Path.Combine(Path.GetTempPath(), "cc-uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(Application.ExecutablePath, temp, true);

            ProcessStartInfo psi = new ProcessStartInfo(temp);
            psi.Arguments = "/cleanup \"" + dir + "\"";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            Process.Start(psi);
        }

        /// <summary>由临时副本执行：删除安装目录（含自身旧副本）。</summary>
        public static void Cleanup(string dir)
        {
            System.Threading.Thread.Sleep(500);
            for (int i = 0; i < 24; i++)
            {
                try
                {
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                    if (!Directory.Exists(dir)) break;
                }
                catch { }
                System.Threading.Thread.Sleep(250);
            }

            if (Directory.Exists(dir))
            {
                try
                {
                    foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                        MoveFileEx(f, null, MOVEFILE_DELAY_UNTIL_REBOOT);
                    MoveFileEx(dir, null, MOVEFILE_DELAY_UNTIL_REBOOT);
                }
                catch { }
            }

            ScheduleSelfDelete();
        }

        private static void ScheduleSelfDelete()
        {
            try
            {
                string self = Application.ExecutablePath;
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe");
                psi.Arguments = "/c ping -n 3 127.0.0.1 >nul & del /f /q \"" + self + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch { }
        }

        private static void ExtractPayload(string exePath)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadName))
            {
                if (s == null) throw new InvalidOperationException("安装包损坏：找不到内置程序。");
                using (FileStream fs = new FileStream(exePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    s.CopyTo(fs);
            }
        }

        private static bool EnsureAppClosed(bool silent)
        {
            Process[] ps = Process.GetProcessesByName(AppName);
            if (ps.Length == 0) return true;

            if (!silent)
            {
                DialogResult r = MessageBox.Show(
                    "C盘清理助手正在运行，需要先关闭。\r\n\r\n是否立即结束它？",
                    AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return false;
            }

            foreach (Process p in ps)
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(3000);
                }
                catch { }
            }
            return true;
        }

        // ---------------------------------------------------------- 快捷方式
        public static string StartMenuShortcut()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");
        }

        public static string UninstallShortcut()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "卸载 " + AppName + ".lnk");
        }

        public static string DesktopShortcut()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk");
        }

        private static void CreateShortcut(string linkPath, string target, string workingDir)
        {
            try
            {
                string linkDir = Path.GetDirectoryName(linkPath);
                if (!Directory.Exists(linkDir)) Directory.CreateDirectory(linkDir);

                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return;
                object shell = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod,
                    null, shell, new object[] { linkPath });

                Type sct = sc.GetType();
                sct.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { target });
                sct.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { workingDir });
                sct.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { target + ",0" });
                sct.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { AppName });
                sct.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);
            }
            catch { }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        // ---------------------------------------------------------- 注册表
        private static void WriteUninstallEntry(string dir, string exe, string uninstall)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RegKey))
            {
                k.SetValue("DisplayName", AppName);
                k.SetValue("DisplayVersion", Version);
                k.SetValue("Publisher", Publisher);
                k.SetValue("DisplayIcon", exe + ",0");
                k.SetValue("InstallLocation", dir);
                k.SetValue("UninstallString", "\"" + uninstall + "\" /uninstall");
                k.SetValue("QuietUninstallString", "\"" + uninstall + "\" /uninstall /S");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)Math.Max(1, DirSize(dir) / 1024), RegistryValueKind.DWord);
            }
        }

        private static void RemoveUninstallEntry()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(RegKey, false); }
            catch { }
        }

        private static long DirSize(string dir)
        {
            long total = 0;
            try
            {
                foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(f).Length; } catch { }
                }
            }
            catch { }
            return total;
        }
    }
}
