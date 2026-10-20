using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CCleaner.Setup
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static int Main(string[] args)
        {
            try { SetProcessDPIAware(); }
            catch { }

            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                    Theme.DpiScale = g.DpiX / 96f;
            }
            catch { Theme.DpiScale = 1f; }

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "/cleanup", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    Installer.Cleanup(args[i + 1].Trim('"'));
                    return 0;
                }
            }

            bool silent = Has(args, "/S");
            bool uninstall = Has(args, "/uninstall");

            if (uninstall)
            {
                if (silent)
                {
                    try { Installer.Uninstall(true); }
                    catch (Exception ex) { WriteError(ex); return 1; }
                    return 0;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SetupForm(true));
                return 0;
            }

            if (silent)
            {
                string dir = Value(args, "/D=");
                bool desktop = Value(args, "/DESKTOP=") == "1";
                bool launch = Value(args, "/LAUNCH=") == "1";
                try
                {
                    Installer.Install(string.IsNullOrEmpty(dir) ? Installer.DefaultDir : dir, desktop, launch, false);
                }
                catch (Exception ex)
                {
                    WriteError(ex);
                    return 1;
                }
                return 0;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm(false));
            return 0;
        }

        private static bool Has(string[] args, string name)
        {
            foreach (string a in args)
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string Value(string[] args, string prefix)
        {
            foreach (string a in args)
            {
                if (a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return a.Substring(prefix.Length).Trim().Trim('"');
            }
            return "";
        }

        private static void WriteError(Exception ex)
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), "c-drive-cleaner-setup-error.txt");
                File.WriteAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + ex.ToString());
            }
            catch { }
        }
    }
}
