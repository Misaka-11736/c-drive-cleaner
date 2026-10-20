using System;
using System.Collections.Generic;
using System.IO;
using CCleaner;

internal static class CleanTest
{
    private static int Main(string[] args)
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "_cctest_gui");
        if (Directory.Exists(sandbox)) Directory.Delete(sandbox, true);
        Directory.CreateDirectory(Path.Combine(sandbox, "a"));
        Directory.CreateDirectory(Path.Combine(sandbox, "b", "c"));
        File.WriteAllBytes(Path.Combine(sandbox, "a", "1.bin"), new byte[1024 * 1024]);
        File.WriteAllBytes(Path.Combine(sandbox, "b", "c", "2.bin"), new byte[2 * 1024 * 1024]);
        string ro = Path.Combine(sandbox, "ro.txt");
        File.WriteAllText(ro, "x");
        File.SetAttributes(ro, FileAttributes.ReadOnly);

        CleanTarget t = new CleanTarget();
        t.Id = "TEST-01";
        t.Label = "沙箱";
        t.Mode = TargetMode.Contents;
        t.Deletable = true;
        t.Paths.Add(sandbox);

        string outDir = Path.Combine(Path.GetTempPath(), "cc-clean-test");
        Cleaner cleaner = new Cleaner(outDir, Environment.GetEnvironmentVariable("USERPROFILE"));
        List<CleanTarget> items = new List<CleanTarget>();
        items.Add(t);
        CleanResult r = cleaner.Clean(items, false, null, System.Threading.CancellationToken.None);

        Console.WriteLine("freed=" + r.Freed + " errors=" + r.Errors + " log=" + r.LogPath);
        Console.WriteLine("root exists=" + Directory.Exists(sandbox) +
            " children=" + (Directory.Exists(sandbox) ? Directory.GetFileSystemEntries(sandbox).Length : -1));
        if (File.Exists(r.LogPath)) Console.WriteLine("log lines=" + File.ReadAllLines(r.LogPath).Length);

        CleanTarget bad = new CleanTarget();
        bad.Id = "BAD";
        bad.Mode = TargetMode.Contents;
        bad.Deletable = true;
        bad.Paths.Add(@"C:\Windows\System32");
        List<CleanTarget> badList = new List<CleanTarget>();
        badList.Add(bad);
        try
        {
            Cleaner.Validate(badList);
            Console.WriteLine("SAFETY FAILED");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine("safety ok: " + ex.Message.Split('\n')[0]);
        }

        // 演练模式：不应删除
        Directory.CreateDirectory(sandbox);
        File.WriteAllText(Path.Combine(sandbox, "keep.txt"), "keep");
        CleanResult d = cleaner.Clean(items, true, null, System.Threading.CancellationToken.None);
        Console.WriteLine("dryrun kept=" + File.Exists(Path.Combine(sandbox, "keep.txt")) + " freed=" + d.Freed);

        return 0;
    }
}
