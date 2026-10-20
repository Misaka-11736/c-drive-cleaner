using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using CCleaner;

internal static class SelfTest
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = new UTF8Encoding(false); }
        catch { }

        string outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "cc-selftest");
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        string up = Environment.GetEnvironmentVariable("USERPROFILE");
        string la = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        string ra = Environment.GetEnvironmentVariable("APPDATA");

        Console.WriteLine("扫描开始: " + DateTime.Now.ToString("HH:mm:ss"));
        Scanner scanner = new Scanner(up, la, ra);
        Progress<ScanProgress> progress = new Progress<ScanProgress>(delegate(ScanProgress p)
        {
            Console.WriteLine(string.Format("  {0} 文件 / {1} 目录 / {2}", Fmt.Count(p.Files), Fmt.Count(p.Dirs), Fmt.Size(p.Bytes)));
        });

        DateTime t0 = DateTime.Now;
        ScanResult r = scanner.Scan(progress, CancellationToken.None);
        Console.WriteLine(string.Format("扫描完成: {0:0.0}s, 文件 {1}, 目录 {2}, 合计 {3}",
            r.DurationSec, Fmt.Count(r.TotalFiles), Fmt.Count(r.DirCount), Fmt.Size(r.TotalBytes)));
        Console.WriteLine(string.Format("可清理 {0} / 推荐 {1} / 仅统计 {2}", Fmt.Size(r.CleanBytes), Fmt.Size(r.RecBytes), Fmt.Size(r.ReportBytes)));

        foreach (CategoryResult c in r.Categories)
            Console.WriteLine(string.Format("  {0,-10} clean={1,10} report={2,10} items={3}", c.Def.Name, Fmt.Size(c.CleanBytes), Fmt.Size(c.ReportBytes), c.Items.Count));

        foreach (Suggestion s in r.Suggestions)
            Console.WriteLine("  建议: " + s.Title);

        string plan = Path.Combine(outDir, "plan.json");
        string html = Path.Combine(outDir, "report.html");
        PlanStore.Save(r, plan);
        HtmlReport.Save(r, html);
        Console.WriteLine("plan: " + plan + " (" + new FileInfo(plan).Length + " bytes)");
        Console.WriteLine("html: " + html + " (" + new FileInfo(html).Length + " bytes)");

        List<CleanTarget> loaded = PlanStore.Load(plan);
        Console.WriteLine("回读计划: " + loaded.Count + " 项");
        int withPaths = 0;
        foreach (CleanTarget t in loaded) if (t.Paths.Count > 0) withPaths++;
        Console.WriteLine("其中有路径的: " + withPaths);

        return 0;
    }
}
