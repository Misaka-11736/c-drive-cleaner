using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using CCleaner;
using CCleaner.Disks;

internal static class DedupeTest
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string newLink, string existing, IntPtr security);

    private static int Main()
    {
        string rootA = Path.Combine(Path.GetTempPath(), "cc-dedupe-a-" + Guid.NewGuid().ToString("N"));
        string rootB = Path.Combine(Path.GetTempPath(), "cc-dedupe-b-" + Guid.NewGuid().ToString("N"));

        if (Directory.Exists(@"D:\") && Directory.Exists(@"E:\"))
        {
            rootA = @"D:\_cc_dedupe_test";
            rootB = @"E:\_cc_dedupe_test";
        }

        Cleanup(rootA);
        Cleanup(rootB);
        Directory.CreateDirectory(rootA);
        Directory.CreateDirectory(rootB);

        try
        {
            byte[] data = Pattern(2 * 1024 * 1024, 0x41);
            File.WriteAllBytes(Path.Combine(rootA, "a.bin"), data);
            File.WriteAllBytes(Path.Combine(rootB, "b.bin"), data);
            File.WriteAllBytes(Path.Combine(rootA, "c.bin"), data);

            File.WriteAllBytes(Path.Combine(rootA, "u.bin"), Pattern(2 * 1024 * 1024, 0x42));

            byte[] small = Pattern(512 * 1024, 0x43);
            File.WriteAllBytes(Path.Combine(rootA, "small1.bin"), small);
            File.WriteAllBytes(Path.Combine(rootB, "small2.bin"), small);

            string h1 = Path.Combine(rootA, "h1.bin");
            string h2 = Path.Combine(rootA, "h2.bin");
            File.WriteAllBytes(h1, Pattern(2 * 1024 * 1024, 0x44));
            bool linked = CreateHardLink(h2, h1, IntPtr.Zero);

            DedupeResult r = new AllVolumeDeduper(new string[] { rootA, rootB }).Scan(null, CancellationToken.None);

            Console.WriteLine(string.Format("files={0} candidates={1} groups={2} reclaimable={3} hardlinkGroups={4}",
                r.TotalFiles, r.CandidateFiles, r.Groups.Count, Fmt.Size(r.ReclaimableBytes), r.HardlinkGroups));

            DedupeGroup content = null;
            DedupeGroup hard = null;
            foreach (DedupeGroup g in r.Groups)
            {
                if (g.HardlinkOnly) hard = g;
                else if (g.Files.Count >= 3) content = g;
            }

            if (content == null || content.Files.Count != 3 || content.DistinctIds != 3 ||
                content.Reclaimable != 2 * 2 * 1024 * 1024)
            {
                Console.WriteLine("FAIL: content group wrong");
                return 1;
            }

            if (linked)
            {
                if (hard == null || hard.Files.Count != 2 || hard.Reclaimable != 0)
                {
                    Console.WriteLine("FAIL: hardlink group wrong");
                    return 1;
                }
            }
            else
            {
                Console.WriteLine("WARN: hardlink creation failed, hardlink assertion skipped");
            }

            if (r.CandidateFiles != (linked ? 6 : 5))
            {
                Console.WriteLine("FAIL: candidate count wrong (threshold or walk)");
                return 1;
            }

            Console.WriteLine("dedupe tests passed");
            return 0;
        }
        finally
        {
            Cleanup(rootA);
            Cleanup(rootB);
        }
    }

    private static byte[] Pattern(int size, byte seed)
    {
        byte[] b = new byte[size];
        for (int i = 0; i < b.Length; i++) b[i] = (byte)(seed + (i % 17));
        return b;
    }

    private static void Cleanup(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch { }
    }
}
