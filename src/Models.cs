using System;
using System.Collections.Generic;
using System.Drawing;

namespace CCleaner
{
    public enum TargetMode { Contents, Path, Dism, Shadow, Recycle, None }

    public enum RiskLevel { Safe, Low, Medium, High }

    public static class Fmt
    {
        public static string Size(long bytes)
        {
            double b = bytes;
            if (b >= 1099511627776.0) return (b / 1099511627776.0).ToString("N2") + " TB";
            if (b >= 1073741824.0) return (b / 1073741824.0).ToString("N2") + " GB";
            if (b >= 1048576.0) return (b / 1048576.0).ToString("N1") + " MB";
            if (b >= 1024.0) return (b / 1024.0).ToString("N0") + " KB";
            return bytes.ToString("N0") + " B";
        }

        public static string Count(long n)
        {
            return n.ToString("N0");
        }

        public static string ShortCount(long n)
        {
            if (n >= 100000000L) return (n / 100000000.0).ToString("0.#") + " 亿";
            if (n >= 10000L) return (n / 10000.0).ToString("0.#") + " 万";
            return n.ToString("N0");
        }

        public static string Pct(double v)
        {
            return v.ToString("0.0") + "%";
        }

        public static string Duration(TimeSpan t)
        {
            if (t.TotalHours >= 1)
                return string.Format("{0}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds);
            return string.Format("{0}:{1:00}", (int)t.TotalMinutes, t.Seconds);
        }
    }

    public static class RiskText
    {
        public static string Of(RiskLevel r)
        {
            switch (r)
            {
                case RiskLevel.Safe: return "安全";
                case RiskLevel.Low: return "低";
                case RiskLevel.Medium: return "中";
                default: return "高";
            }
        }

        public static Color Back(RiskLevel r)
        {
            switch (r)
            {
                case RiskLevel.Safe: return Color.FromArgb(232, 247, 238);
                case RiskLevel.Low: return Color.FromArgb(238, 242, 255);
                case RiskLevel.Medium: return Color.FromArgb(255, 244, 229);
                default: return Color.FromArgb(253, 236, 236);
            }
        }

        public static Color Fore(RiskLevel r)
        {
            switch (r)
            {
                case RiskLevel.Safe: return Color.FromArgb(15, 123, 69);
                case RiskLevel.Low: return Color.FromArgb(59, 79, 216);
                case RiskLevel.Medium: return Color.FromArgb(163, 91, 0);
                default: return Color.FromArgb(180, 35, 24);
            }
        }
    }

    public class CleanTarget
    {
        public string Id = "";
        public string Cat = "";
        public string Label = "";
        public string PathPattern = "";
        public TargetMode Mode = TargetMode.Contents;
        public RiskLevel Risk = RiskLevel.Safe;
        public bool Admin;
        public bool Recommend;
        public string Note = "";
        public bool Deletable = true;
        public bool Selected;

        public long Bytes;
        public long Files;
        public List<string> Paths = new List<string>();
        public bool Exists;
        public bool Denied;
        public DateTime LastWrite;

        public string ModeText
        {
            get
            {
                switch (Mode)
                {
                    case TargetMode.Contents: return "contents";
                    case TargetMode.Path: return "path";
                    case TargetMode.Dism: return "dism";
                    case TargetMode.Shadow: return "shadow";
                    case TargetMode.Recycle: return "recycle";
                    default: return "none";
                }
            }
        }

        public static TargetMode ParseMode(string s)
        {
            if (s == null) return TargetMode.None;
            switch (s.ToLowerInvariant())
            {
                case "contents": return TargetMode.Contents;
                case "path": return TargetMode.Path;
                case "dism": return TargetMode.Dism;
                case "shadow": return TargetMode.Shadow;
                case "recycle": return TargetMode.Recycle;
                default: return TargetMode.None;
            }
        }
    }

    public class CategoryDef
    {
        public string Id = "";
        public string Name = "";
        public string Desc = "";
        public Color Color = Color.Gray;
    }

    public class CategoryResult
    {
        public CategoryDef Def;
        public List<CleanTarget> Items = new List<CleanTarget>();
        public long CleanBytes;
        public long RecBytes;
        public long ReportBytes;
    }

    public class DirItem
    {
        public string Path = "";
        public long Bytes;
        public int Depth;
    }

    public class FileItem
    {
        public string Path = "";
        public long Bytes;
        public DateTime Modified;
    }

    public class Suggestion
    {
        public string Title = "";
        public string Body = "";
        public long Bytes;
    }

    public class ScanResult
    {
        public DateTime GeneratedAt = DateTime.Now;
        public double DurationSec;
        public bool Elevated;
        public string Computer = Environment.MachineName;
        public string User = Environment.UserName;
        public long DriveTotal;
        public long DriveFree;
        public long DriveUsed;
        public long TotalBytes;
        public long TotalFiles;
        public long DirCount;
        public long DeniedCount;
        public List<string> DeniedPaths = new List<string>();
        public List<CategoryResult> Categories = new List<CategoryResult>();
        public List<CleanTarget> Targets = new List<CleanTarget>();
        public List<DirItem> TopDirs = new List<DirItem>();
        public List<FileItem> TopFiles = new List<FileItem>();
        public List<Suggestion> Suggestions = new List<Suggestion>();
        public long CleanBytes;
        public long RecBytes;
        public long ReportBytes;
        public long WinSxSBytes;
        public string PlanPath = "";
        public string HtmlPath = "";

        public double FreePercent
        {
            get { return DriveTotal > 0 ? DriveFree * 100.0 / DriveTotal : 0; }
        }

        public List<CleanTarget> DeletableItems()
        {
            List<CleanTarget> list = new List<CleanTarget>();
            foreach (CleanTarget t in Targets)
                if (t.Exists && t.Deletable) list.Add(t);
            return list;
        }

        public List<CleanTarget> ReportOnlyItems()
        {
            List<CleanTarget> list = new List<CleanTarget>();
            foreach (CleanTarget t in Targets)
                if (t.Exists && !t.Deletable) list.Add(t);
            return list;
        }

        public CategoryResult CategoryOf(string id)
        {
            foreach (CategoryResult c in Categories)
                if (c.Def.Id == id) return c;
            return null;
        }
    }

    public class ScanProgress
    {
        public long Files;
        public long Dirs;
        public long Bytes;
        public string Current = "";
    }

    public class CleanProgress
    {
        public int Index;
        public int Total;
        public string ItemId = "";
        public string Message = "";
    }

    public class CleanResult
    {
        public long BeforeFree;
        public long AfterFree;
        public int Errors;
        public List<string> ErrorMessages = new List<string>();
        public string LogPath = "";
        public bool Canceled;
        public bool DryRun;

        public long Freed
        {
            get { return AfterFree - BeforeFree; }
        }
    }
}
