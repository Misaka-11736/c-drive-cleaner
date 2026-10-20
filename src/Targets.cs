using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace CCleaner
{
    public static class TargetCatalog
    {
        public static List<CategoryDef> CreateCategories()
        {
            List<CategoryDef> list = new List<CategoryDef>();
            list.Add(Cat("sys", "系统垃圾", "临时文件、更新缓存、日志、转储、回收站", "#2f6fed"));
            list.Add(Cat("dev", "开发与运行缓存", "npm / pnpm / pip / NuGet / Gradle / 浏览器缓存", "#7c4dff"));
            list.Add(Cat("comp", "系统组件", "WinSxS 组件存储、系统还原点（清理耗时较长）", "#e8a13a"));
            list.Add(Cat("game", "游戏与大型软件", "GPU / Steam 着色器缓存、下载缓存；游戏库本体仅统计", "#12a67a"));
            list.Add(Cat("app", "第三方应用缓存", "微信 / QQ / Office / 驱动安装包", "#d9538a"));
            list.Add(Cat("user", "用户大文件", "下载 / 桌面 / 文档 / 视频（仅统计，不删除）", "#5b6b7f"));
            return list;
        }

        private static CategoryDef Cat(string id, string name, string desc, string color)
        {
            CategoryDef c = new CategoryDef();
            c.Id = id;
            c.Name = name;
            c.Desc = desc;
            c.Color = ColorTranslator.FromHtml(color);
            return c;
        }

        private static CleanTarget T(string id, string cat, string label, string path, TargetMode mode,
            RiskLevel risk, bool admin, bool rec, string note, bool deletable)
        {
            CleanTarget t = new CleanTarget();
            t.Id = id;
            t.Cat = cat;
            t.Label = label;
            t.PathPattern = path;
            t.Mode = mode;
            t.Risk = risk;
            t.Admin = admin;
            t.Recommend = rec;
            t.Note = note;
            t.Deletable = deletable;
            return t;
        }

        public static List<CleanTarget> Create(string userProfile, string localAppData, string appData)
        {
            List<CleanTarget> t = new List<CleanTarget>();
            string U = userProfile;
            string L = localAppData;
            string A = appData;
            string usersRoot = Path.GetPathRoot(userProfile) + "Users";
            string userTemp = Path.Combine(usersRoot, "*", "AppData", "Local", "Temp");
            string userDump = Path.Combine(usersRoot, "*", "AppData", "Local", "CrashDumps");

            // ---------------- 系统垃圾 ----------------
            t.Add(T("SYS-01", "sys", "Windows 临时文件", @"C:\Windows\Temp", TargetMode.Contents, RiskLevel.Safe, true, true,
                "系统与安装程序临时文件，占用中的会自动跳过", true));
            t.Add(T("SYS-02", "sys", "用户临时文件", userTemp, TargetMode.Contents, RiskLevel.Safe, false, true,
                "各用户临时文件，可安全删除", true));
            t.Add(T("SYS-03", "sys", "Windows 更新缓存", @"C:\Windows\SoftwareDistribution\Download", TargetMode.Contents, RiskLevel.Safe, true, true,
                "已下载的更新包，需要时会重新下载", true));
            t.Add(T("SYS-04", "sys", "传递优化缓存", @"C:\ProgramData\Microsoft\Windows\DeliveryOptimization", TargetMode.Contents, RiskLevel.Safe, true, true,
                "更新分发 P2P 缓存", true));
            t.Add(T("SYS-05", "sys", "错误报告存档", @"C:\ProgramData\Microsoft\Windows\WER", TargetMode.Contents, RiskLevel.Safe, true, true,
                "Windows 错误报告队列与存档", true));
            t.Add(T("SYS-06", "sys", "内核崩溃转储", @"C:\Windows\Minidump", TargetMode.Contents, RiskLevel.Low, true, true,
                "蓝屏小转储，普通用户无用", true));
            t.Add(T("SYS-07", "sys", "内核实时报告", @"C:\Windows\LiveKernelReports", TargetMode.Contents, RiskLevel.Low, true, true,
                "内核实时转储", true));
            t.Add(T("SYS-08", "sys", "完整内存转储", @"C:\Windows\MEMORY.DMP", TargetMode.Path, RiskLevel.Low, true, true,
                "完整蓝屏转储，通常数百 MB 至数 GB", true));
            t.Add(T("SYS-09", "sys", "系统日志", @"C:\Windows\Logs", TargetMode.Contents, RiskLevel.Low, true, true,
                "CBS / DISM / 更新等日志", true));
            t.Add(T("SYS-10", "sys", "系统 IIS 日志", @"C:\Windows\System32\LogFiles", TargetMode.Contents, RiskLevel.Low, true, true,
                "HTTP 错误日志与跟踪日志", true));
            t.Add(T("SYS-11", "sys", "安装日志", @"C:\Windows\Panther", TargetMode.Contents, RiskLevel.Low, true, true,
                "系统安装 / 升级日志", true));
            t.Add(T("SYS-12", "sys", "回收站", @"C:\$Recycle.Bin", TargetMode.Recycle, RiskLevel.Low, true, true,
                "清空 C 盘回收站（含其他用户）", true));
            t.Add(T("SYS-13", "sys", "系统重置残留", @"C:\$SysReset", TargetMode.Contents, RiskLevel.Medium, true, true,
                "系统重置 / 回滚残留，确认不再回滚后可删", true));
            t.Add(T("SYS-14", "sys", "性能日志", @"C:\PerfLogs", TargetMode.Contents, RiskLevel.Safe, false, true,
                "性能计数器日志，通常为空", true));
            t.Add(T("SYS-15", "sys", "IIS 站点日志", @"C:\inetpub\logs", TargetMode.Contents, RiskLevel.Low, false, true,
                "未运行 IIS 时可删", true));
            t.Add(T("SYS-16", "sys", "预读取数据", @"C:\Windows\Prefetch", TargetMode.Contents, RiskLevel.Low, true, false,
                "删除后程序首次启动略慢，收益小", true));
            t.Add(T("SYS-17", "sys", "OneDrive 临时", @"C:\OneDriveTemp", TargetMode.Contents, RiskLevel.Low, false, false,
                "OneDrive 同步临时文件", true));
            t.Add(T("SYS-18", "sys", "用户崩溃转储", userDump, TargetMode.Contents, RiskLevel.Low, false, true,
                "应用程序崩溃转储", true));

            // ---------------- 开发与运行缓存 ----------------
            t.Add(T("DEV-01", "dev", "npm 缓存", Path.Combine(L, "npm-cache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "npm 包缓存，可重新下载", true));
            t.Add(T("DEV-02", "dev", "npm 漫游缓存", Path.Combine(A, "npm-cache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "旧版 npm 缓存位置", true));
            t.Add(T("DEV-03", "dev", "pnpm 存储", Path.Combine(L, "pnpm", "store"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "pnpm 内容寻址存储，可重新下载", true));
            t.Add(T("DEV-04", "dev", "pnpm 缓存", Path.Combine(L, "pnpm-cache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "pnpm 元数据缓存", true));
            t.Add(T("DEV-05", "dev", "Yarn 缓存", Path.Combine(L, "Yarn", "Cache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "Yarn 包缓存", true));
            t.Add(T("DEV-06", "dev", "pip 缓存", Path.Combine(L, "pip", "Cache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "Python 包缓存", true));
            t.Add(T("DEV-07", "dev", "NuGet 缓存", Path.Combine(L, "NuGet", "v3-cache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "NuGet 包缓存", true));
            t.Add(T("DEV-08", "dev", "NuGet 全局包", Path.Combine(U, ".nuget", "packages"), TargetMode.Contents, RiskLevel.Medium, false, true,
                "已还原的包，删除后项目需重新还原", true));
            t.Add(T("DEV-09", "dev", "Gradle 缓存", Path.Combine(U, ".gradle", "caches"), TargetMode.Contents, RiskLevel.Medium, false, true,
                "Gradle 依赖与构建缓存", true));
            t.Add(T("DEV-10", "dev", "Maven 仓库", Path.Combine(U, ".m2", "repository"), TargetMode.Contents, RiskLevel.Medium, false, true,
                "Maven 本地仓库，可重新下载", true));
            t.Add(T("DEV-11", "dev", "Go 模块缓存", Path.Combine(U, "go", "pkg", "mod"), TargetMode.Contents, RiskLevel.Medium, false, true,
                "Go module 缓存", true));
            t.Add(T("DEV-12", "dev", "Cargo 注册表缓存", Path.Combine(U, ".cargo", "registry", "cache"), TargetMode.Contents, RiskLevel.Low, false, true,
                "Rust crate 下载缓存", true));
            t.Add(T("DEV-13", "dev", "Docker 数据盘", Path.Combine(L, "Docker", "wsl", "data", "ext4.vhdx"), TargetMode.Path, RiskLevel.High, false, false,
                "仅统计；内含镜像与容器，删除会全部丢失", false));
            t.Add(T("DEV-14", "dev", "VS Code 缓存", Path.Combine(A, "Code", "Cache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "VS Code 渲染缓存", true));
            t.Add(T("DEV-15", "dev", "VS Code 代码缓存", Path.Combine(A, "Code", "CachedData"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "VS Code 编译缓存", true));
            t.Add(T("DEV-16", "dev", "VS Code 日志", Path.Combine(A, "Code", "logs"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "VS Code 运行日志", true));
            t.Add(T("DEV-17", "dev", "JetBrains 缓存", Path.Combine(L, "JetBrains", "*", "caches"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "IDEA / PyCharm 等索引缓存", true));
            t.Add(T("DEV-18", "dev", "Edge 缓存", Path.Combine(L, "Microsoft", "Edge", "User Data", "*", "Cache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "网页缓存，不影响书签与密码", true));
            t.Add(T("DEV-19", "dev", "Edge 代码缓存", Path.Combine(L, "Microsoft", "Edge", "User Data", "*", "Code Cache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "JS 字节码缓存", true));
            t.Add(T("DEV-20", "dev", "Edge GPU 缓存", Path.Combine(L, "Microsoft", "Edge", "User Data", "*", "GPUCache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "显卡着色器缓存", true));
            t.Add(T("DEV-21", "dev", "Chrome 缓存", Path.Combine(L, "Google", "Chrome", "User Data", "*", "Cache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "网页缓存，不影响书签与密码", true));
            t.Add(T("DEV-22", "dev", "Chrome 代码缓存", Path.Combine(L, "Google", "Chrome", "User Data", "*", "Code Cache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "JS 字节码缓存", true));
            t.Add(T("DEV-23", "dev", "Chrome GPU 缓存", Path.Combine(L, "Google", "Chrome", "User Data", "*", "GPUCache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "显卡着色器缓存", true));
            t.Add(T("DEV-24", "dev", "Firefox 缓存", Path.Combine(L, "Mozilla", "Firefox", "Profiles", "*", "cache2"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "网页缓存", true));
            t.Add(T("DEV-25", "dev", "用户缓存目录", Path.Combine(U, ".cache"), TargetMode.Contents, RiskLevel.Low, false, true,
                "各类工具的运行/下载缓存（含运行时），可重建", true));

            // ---------------- 系统组件 ----------------
            t.Add(T("COMP-01", "comp", "WinSxS 组件存储", @"C:\Windows\WinSxS", TargetMode.Dism, RiskLevel.Safe, true, true,
                "DISM 组件清理通常释放 1-4 GB，耗时较长", true));
            t.Add(T("COMP-02", "comp", "系统还原点", "", TargetMode.Shadow, RiskLevel.Medium, true, true,
                "删除除最新一个以外的还原点", true));

            // ---------------- 游戏与大型软件 ----------------
            t.Add(T("GAME-01", "game", "Steam 着色器缓存", @"C:\SteamLibrary\steamapps\shadercache", TargetMode.Contents, RiskLevel.Safe, false, true,
                "游戏着色器预编译缓存，会重建", true));
            t.Add(T("GAME-02", "game", "Steam 下载缓存", @"C:\SteamLibrary\steamapps\downloading", TargetMode.Contents, RiskLevel.Safe, false, true,
                "未完成的下载分块", true));
            t.Add(T("GAME-03", "game", "Steam 安装临时", @"C:\SteamLibrary\steamapps\temp", TargetMode.Contents, RiskLevel.Safe, false, true,
                "安装临时文件", true));
            t.Add(T("GAME-04", "game", "Steam 游戏库本体", @"C:\SteamLibrary\steamapps\common", TargetMode.None, RiskLevel.Medium, false, false,
                "仅统计；建议用 Steam「设置-存储」迁移到 D 盘", false));
            t.Add(T("GAME-05", "game", "Xbox 游戏", @"C:\XboxGames", TargetMode.None, RiskLevel.Medium, false, false,
                "仅统计；在「设置-应用」中迁移到 D 盘", false));
            t.Add(T("GAME-06", "game", "Epic 网络缓存", Path.Combine(L, "EpicGamesLauncher", "Saved", "webcache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "启动器网页缓存", true));
            t.Add(T("GAME-07", "game", "Steam 客户端缓存", @"C:\Program Files (x86)\Steam\appcache", TargetMode.Contents, RiskLevel.Safe, false, true,
                "客户端网页与图标缓存", true));
            t.Add(T("GAME-08", "game", "NVIDIA 着色器缓存", Path.Combine(L, "NVIDIA", "DXCache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "DirectX 着色器缓存，可重建；驱动缺陷会让它膨胀到几十 GB", true));
            t.Add(T("GAME-09", "game", "NVIDIA GL 缓存", Path.Combine(L, "NVIDIA", "GLCache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "OpenGL 着色器缓存，可重建", true));
            t.Add(T("GAME-10", "game", "DirectX 着色器缓存", Path.Combine(L, "D3DSCache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "系统级 D3D 着色器缓存，可重建", true));
            t.Add(T("GAME-11", "game", "NVIDIA App 更新缓存", @"C:\ProgramData\NVIDIA Corporation\NVIDIA App\UpdateFramework", TargetMode.Contents, RiskLevel.Low, false, true,
                "NVIDIA App 下载的驱动/更新安装包，可按需重新下载", true));

            // ---------------- 第三方应用缓存 ----------------
            t.Add(T("APP-01", "app", "微信文件", Path.Combine(U, "Documents", "WeChat Files"), TargetMode.None, RiskLevel.High, false, false,
                "仅统计；聊天记录与文件，请勿直接删除", false));
            t.Add(T("APP-02", "app", "微信新版文件", Path.Combine(U, "Documents", "xwechat_files"), TargetMode.None, RiskLevel.High, false, false,
                "仅统计；聊天记录与文件，请勿直接删除", false));
            t.Add(T("APP-03", "app", "QQ 文件", Path.Combine(U, "Documents", "Tencent Files"), TargetMode.None, RiskLevel.High, false, false,
                "仅统计；聊天记录与文件，请勿直接删除", false));
            t.Add(T("APP-04", "app", "钉钉文件", Path.Combine(U, "Documents", "DingTalk"), TargetMode.None, RiskLevel.High, false, false,
                "仅统计；聊天记录与文件，请勿直接删除", false));
            t.Add(T("APP-05", "app", "Office 文件缓存", Path.Combine(L, "Microsoft", "Office", "16.0", "OfficeFileCache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "Office 文档缓存", true));
            t.Add(T("APP-06", "app", "Teams 缓存", Path.Combine(A, "Microsoft", "Teams", "Cache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "Teams 网页缓存", true));
            t.Add(T("APP-07", "app", "Teams 代码缓存", Path.Combine(A, "Microsoft", "Teams", "Code Cache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "Teams JS 缓存", true));
            t.Add(T("APP-08", "app", "Adobe 媒体缓存", Path.Combine(L, "Adobe", "Common", "Media Cache Files"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "Premiere / AE 媒体缓存", true));
            t.Add(T("APP-09", "app", "Adobe 媒体缓存 2", Path.Combine(L, "Adobe", "Common", "Media Cache"), TargetMode.Contents, RiskLevel.Safe, false, true,
                "Premiere / AE 媒体缓存", true));
            t.Add(T("APP-10", "app", "NVIDIA 安装包", @"C:\NVIDIA", TargetMode.Contents, RiskLevel.Safe, false, true,
                "显卡驱动安装包残留", true));
            t.Add(T("APP-11", "app", "NVIDIA 下载缓存", @"C:\ProgramData\NVIDIA Corporation\Downloader", TargetMode.Contents, RiskLevel.Safe, false, true,
                "驱动下载缓存", true));
            t.Add(T("APP-12", "app", "AMD 安装包", @"C:\AMD", TargetMode.Contents, RiskLevel.Safe, false, true,
                "显卡驱动安装包残留", true));
            t.Add(T("APP-13", "app", "腾讯视频缓存", Path.Combine(A, "Tencent", "QQLive"), TargetMode.None, RiskLevel.Medium, false, false,
                "仅统计；可能包含已下载视频", false));
            t.Add(T("APP-14", "app", "爱奇艺缓存", Path.Combine(L, "iqiyi"), TargetMode.None, RiskLevel.Medium, false, false,
                "仅统计；可能包含已下载视频", false));
            t.Add(T("APP-15", "app", "安装包缓存", @"C:\ProgramData\Package Cache", TargetMode.None, RiskLevel.Medium, false, false,
                "仅统计；用于软件修复/卸载，删除后无法修复", false));

            // ---------------- 用户大文件 ----------------
            t.Add(T("USER-01", "user", "下载文件夹", Path.Combine(U, "Downloads"), TargetMode.None, RiskLevel.High, false, false,
                "仅统计；请自行筛选安装包与旧文件", false));
            t.Add(T("USER-02", "user", "桌面", Path.Combine(U, "Desktop"), TargetMode.None, RiskLevel.High, false, false,
                "仅统计", false));
            t.Add(T("USER-03", "user", "文档", Path.Combine(U, "Documents"), TargetMode.None, RiskLevel.High, false, false,
                "仅统计", false));
            t.Add(T("USER-04", "user", "视频", Path.Combine(U, "Videos"), TargetMode.None, RiskLevel.High, false, false,
                "仅统计", false));
            t.Add(T("USER-05", "user", "OneDrive 目录", Path.Combine(U, "OneDrive"), TargetMode.None, RiskLevel.High, false, false,
                "仅统计；删除会同步到云端，切勿直接删", false));

            return t;
        }
    }
}
