using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CCleaner.Disks
{
    internal static class ModuleProgram
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private static bool Has(string[] args, string name)
        {
            foreach (string a in args)
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string GetArg(string[] args, string prefix)
        {
            foreach (string a in args)
                if (a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return a.Substring(prefix.Length);
            return "";
        }

        [STAThread]
        private static void Main(string[] args)
        {
            try { SetProcessDPIAware(); }
            catch { }

            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                    Theme.DpiScale = g.DpiX / 96f;
            }
            catch { Theme.DpiScale = 1f; }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                DiskForm form = new DiskForm();
                string scan = GetArg(args, "--scan=");
                if (!string.IsNullOrEmpty(scan))
                {
                    bool deep = Has(args, "--deep");
                    form.Shown += delegate { form.SelectAndScan(scan.Trim(), deep); };
                }
                Application.Run(form);
            }
            catch (Exception ex)
            {
                MessageBox.Show("磁盘扩展包启动失败：\r\n" + ex.Message, "磁盘扩展包",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
