using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CCleaner
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string up = Environment.GetEnvironmentVariable("USERPROFILE");
            string la = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            string ra = Environment.GetEnvironmentVariable("APPDATA");
            bool noElevate = false;
            bool autoScan = false;
            bool autoDryRun = false;
            bool noUpdateCheck = false;
            int startPage = 0;
            string openPlan = "";

            foreach (string a in args)
            {
                if (a.StartsWith("--up=", StringComparison.OrdinalIgnoreCase)) up = Unquote(a.Substring(5));
                else if (a.StartsWith("--la=", StringComparison.OrdinalIgnoreCase)) la = Unquote(a.Substring(5));
                else if (a.StartsWith("--ra=", StringComparison.OrdinalIgnoreCase)) ra = Unquote(a.Substring(5));
                else if (a == "--no-elevate") noElevate = true;
                else if (a == "--scan") autoScan = true;
                else if (a == "--dry-run") autoDryRun = true;
                else if (a == "--no-update-check") noUpdateCheck = true;
                else if (a.StartsWith("--page=", StringComparison.OrdinalIgnoreCase)) int.TryParse(a.Substring(7), out startPage);
                else if (a.StartsWith("--plan=", StringComparison.OrdinalIgnoreCase)) openPlan = Unquote(a.Substring(7));
            }

            try { SetProcessDPIAware(); }
            catch { }

            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                    Theme.DpiScale = g.DpiX / 96f;
            }
            catch { Theme.DpiScale = 1f; }

            if (!Util.IsAdmin() && !noElevate)
            {
                if (RelaunchElevated(up, la, ra)) return;
                MessageBox.Show(
                    "C盘清理助手需要管理员权限才能清理系统目录。\r\n\r\n请右键程序，选择「以管理员身份运行」。",
                    "C盘清理助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
            {
                MessageBox.Show("发生未处理的错误：\r\n" + e.Exception.Message,
                    "C盘清理助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                if (ex != null)
                    MessageBox.Show("发生未处理的错误：\r\n" + ex.Message,
                        "C盘清理助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.Run(new MainForm(up, la, ra, autoScan, startPage, openPlan, autoDryRun, noUpdateCheck));
        }

        private static string Unquote(string s)
        {
            return s.Trim().Trim('"');
        }

        private static bool RelaunchElevated(string up, string la, string ra)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath);
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.Arguments = "--up=\"" + up + "\" --la=\"" + la + "\" --ra=\"" + ra + "\"";
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }
}
