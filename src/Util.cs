using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;

namespace CCleaner
{
    public static class Util
    {
        public static bool IsAdmin()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal p = new WindowsPrincipal(id);
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static string LP(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (path.StartsWith(@"\\?\")) return path;
            if (path.Length >= 240 && path.Length > 1 && path[1] == ':') return @"\\?\" + path;
            return path;
        }

        public static string NP(string path)
        {
            if (path != null && path.StartsWith(@"\\?\")) return path.Substring(4);
            return path;
        }

        public static bool IsReparse(string path)
        {
            try
            {
                FileAttributes a = File.GetAttributes(LP(path));
                return (a & FileAttributes.ReparsePoint) != 0;
            }
            catch
            {
                return false;
            }
        }

        public static long FreeSpace(string driveLetter)
        {
            try
            {
                DriveInfo d = new DriveInfo(driveLetter);
                return d.AvailableFreeSpace;
            }
            catch
            {
                return 0;
            }
        }

        public static long TotalSpace(string driveLetter)
        {
            try
            {
                DriveInfo d = new DriveInfo(driveLetter);
                return d.TotalSize;
            }
            catch
            {
                return 0;
            }
        }

        private static readonly string[] ProtectedPaths = new string[]
        {
            @"C:", @"C:\",
            @"C:\Windows",
            @"C:\Windows\System32",
            @"C:\Windows\SysWOW64",
            @"C:\Windows\WinSxS",
            @"C:\Windows\Boot",
            @"C:\Windows\Fonts",
            @"C:\Windows\assembly",
            @"C:\Windows\System32\config",
            @"C:\Windows\System32\drivers",
            @"C:\Windows\System32\DriverStore",
            @"C:\Program Files",
            @"C:\Program Files (x86)",
            @"C:\ProgramData",
            @"C:\Users",
            @"C:\Recovery",
            @"C:\Boot",
            @"C:\EFI",
            @"C:\System Volume Information"
        };

        /// <summary>删除前的白名单校验，任何一条不满足都拒绝。</summary>
        public static bool IsSafeDeletePath(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return false;
            string n = p.TrimEnd('\\');
            if (n.Length == 2 && n[1] == ':') return false;
            if (!n.StartsWith(@"C:\", StringComparison.OrdinalIgnoreCase)) return false;
            if (n.Contains("..")) return false;
            if (n.IndexOfAny(new char[] { '*', '?' }) >= 0) return false;

            foreach (string x in ProtectedPaths)
            {
                if (string.Equals(n, x.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return false;
            }

            string[] segs = n.Substring(3).Split('\\');
            if (segs.Length == 2 && segs[0].Equals("Users", StringComparison.OrdinalIgnoreCase)) return false;
            if (segs.Length == 3 && segs[0].Equals("Users", StringComparison.OrdinalIgnoreCase) &&
                segs[2].Equals("AppData", StringComparison.OrdinalIgnoreCase)) return false;
            if (segs.Length == 4 && segs[0].Equals("Users", StringComparison.OrdinalIgnoreCase) &&
                segs[2].Equals("AppData", StringComparison.OrdinalIgnoreCase))
            {
                string last = segs[3];
                if (last.Equals("Local", StringComparison.OrdinalIgnoreCase) ||
                    last.Equals("LocalLow", StringComparison.OrdinalIgnoreCase) ||
                    last.Equals("Roaming", StringComparison.OrdinalIgnoreCase)) return false;
            }

            if (Directory.Exists(LP(n)) || File.Exists(LP(n)))
            {
                if (IsReparse(n)) return false;
            }
            return true;
        }
    }
}
