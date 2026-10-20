using System;
using System.Collections.Generic;

namespace CCleaner.Disks
{
    public class VolumeInfo
    {
        public string Letter = "";
        public string Root = "";
        public string Label = "";
        public string FileSystem = "";
        public int DriveType;
        public string Serial = "";
        public string BusType = "";
        public long Total;
        public long Free;
        public bool IsExternal;
        public bool IsBackup;
        public bool IsRemovable;

        public string TypeText
        {
            get
            {
                if (IsRemovable) return "U 盘 / 移动设备";
                if (IsExternal) return "USB 移动硬盘";
                return "内置磁盘";
            }
        }

        public string SizeText
        {
            get { return Fmt.Size(Free) + " 可用 / " + Fmt.Size(Total); }
        }

        public double UsedPercent
        {
            get { return Total > 0 ? (Total - Free) * 100.0 / Total : 0; }
        }

        public string StatusText
        {
            get
            {
                if (IsBackup) return "备份盘 · 禁止删除";
                if (IsExternal) return "外接设备 · 勿拔线";
                return "可扫描";
            }
        }
    }

    public enum VolumeItemMode { Contents, Recycle, Report }

    public class VolumeItem
    {
        public string Id = "";
        public string Category = "";
        public string Label = "";
        public string Path = "";
        public string Note = "";
        public VolumeItemMode Mode = VolumeItemMode.Report;
        public bool Deletable;
        public long Bytes;
        public long Files;
        public RiskLevel Risk = RiskLevel.Safe;
        public bool Selected;
    }

    public class DupGroup
    {
        public long Size;
        public List<string> Files = new List<string>();
    }

    public class VolumeScanResult
    {
        public VolumeInfo Volume;
        public bool Deep;
        public DateTime At = DateTime.Now;
        public double Seconds;
        public long TotalBytes;
        public long TotalFiles;
        public long DirCount;
        public List<VolumeItem> Items = new List<VolumeItem>();
        public List<FileItem> TopFiles = new List<FileItem>();
        public List<DirItem> TopDirs = new List<DirItem>();
        public List<DupGroup> Duplicates = new List<DupGroup>();
        public List<string> Denied = new List<string>();
        public long DuplicateBytes;
        public bool Canceled;

        public long DeletableBytes()
        {
            long b = 0;
            foreach (VolumeItem i in Items) if (i.Deletable) b += i.Bytes;
            return b;
        }

        public long ReportBytes()
        {
            long b = 0;
            foreach (VolumeItem i in Items) if (!i.Deletable) b += i.Bytes;
            return b;
        }
    }
}
