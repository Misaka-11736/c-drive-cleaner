using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;

namespace CCleaner
{
    public static class HtmlReport
    {
        public static void Save(ScanResult r, string path)
        {
            StringBuilder sb = new StringBuilder(Template());
            sb.Replace("{{META}}", Enc(Meta(r)));
            sb.Replace("{{DISK}}", Disk(r));
            sb.Replace("{{KPIS}}", Kpis(r));
            sb.Replace("{{CLEAN_SECTIONS}}", CleanSections(r));
            sb.Replace("{{REPORT_ONLY}}", ReportOnly(r));
            sb.Replace("{{TOP_DIRS}}", TopDirs(r));
            sb.Replace("{{TOP_FILES}}", TopFiles(r));
            sb.Replace("{{SUGGESTIONS}}", Suggestions(r));
            sb.Replace("{{NOTES}}", Notes(r));
            sb.Replace("{{FOOTER}}", Footer(r));

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        private static string Template()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream("CCleaner.report-template.html"))
            {
                if (s == null) throw new InvalidOperationException("内置报告模板缺失");
                using (StreamReader sr = new StreamReader(s, Encoding.UTF8))
                    return sr.ReadToEnd();
            }
        }

        private static string Enc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string Meta(ScanResult r)
        {
            string perm = r.Elevated ? "管理员" : "普通用户（部分系统目录未统计）";
            return Enc(r.Computer + " · " + r.User + " · 扫描于 " + r.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss") +
                " · 用时 " + (r.DurationSec / 60.0).ToString("0.0") + " 分钟 · 权限 " + perm);
        }

        private static string Disk(ScanResult r)
        {
            double usedPct = r.DriveTotal > 0 ? (r.DriveTotal - r.DriveFree) * 100.0 / r.DriveTotal : 0;
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"diskrow\"><span>总容量 <b>").Append(Fmt.Size(r.DriveTotal))
              .Append("</b> · 已用 <b>").Append(Fmt.Size(r.DriveUsed))
              .Append("</b></span><span>可用 <b>").Append(Fmt.Size(r.DriveFree)).Append("</b></span></div>");
            sb.Append("<div class=\"diskbar\"><i class=\"").Append(usedPct >= 85 ? "hot" : "").Append("\" style=\"width:")
              .Append(usedPct.ToString("0.0")).Append("%\"></i></div>");
            sb.Append("<div class=\"legend\"><span>已用 ").Append(usedPct.ToString("0.0"))
              .Append("%</span><span>可用 ").Append((100 - usedPct).ToString("0.0"))
              .Append("%</span><span>本次可清理 ").Append(Fmt.Size(r.CleanBytes)).Append("</span></div>");
            return sb.ToString();
        }

        private static string Kpis(ScanResult r)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"kpi\"><div class=\"k\">可清理合计</div><div class=\"v blue\">")
              .Append(Fmt.Size(r.CleanBytes)).Append("</div></div>");
            sb.Append("<div class=\"kpi\"><div class=\"k\">其中推荐项</div><div class=\"v green\">")
              .Append(Fmt.Size(r.RecBytes)).Append("</div></div>");
            sb.Append("<div class=\"kpi\"><div class=\"k\">仅统计不建议删</div><div class=\"v gray\">")
              .Append(Fmt.Size(r.ReportBytes)).Append("</div></div>");
            sb.Append("<div class=\"kpi\"><div class=\"k\">扫描文件 / 目录</div><div class=\"v\">")
              .Append(Fmt.Count(r.TotalFiles)).Append(" / ").Append(Fmt.Count(r.DirCount)).Append("</div></div>");
            return sb.ToString();
        }

        private static string RiskClass(RiskLevel risk)
        {
            switch (risk)
            {
                case RiskLevel.Safe: return "t1";
                case RiskLevel.Low: return "t2";
                case RiskLevel.Medium: return "t3";
                default: return "t4";
            }
        }

        private static string CleanSections(ScanResult r)
        {
            StringBuilder sb = new StringBuilder();
            foreach (CategoryResult c in r.Categories)
            {
                List<CleanTarget> items = new List<CleanTarget>();
                foreach (CleanTarget t in c.Items)
                {
                    if (!t.Deletable) continue;
                    if (t.Bytes == 0 && !t.Denied && t.Mode != TargetMode.Dism && t.Mode != TargetMode.Shadow) continue;
                    items.Add(t);
                }
                if (items.Count == 0) continue;

                string sum = c.CleanBytes == 0 ? "按实际" : Fmt.Size(c.CleanBytes);
                sb.Append("<div class=\"cathead\"><span class=\"dot\" style=\"background:")
                  .Append(ColorTranslator.ToHtml(c.Def.Color)).Append("\"></span><h2>").Append(Enc(c.Def.Name))
                  .Append("</h2><span class=\"csum\">可清理 ").Append(sum).Append("</span><span class=\"cdesc\">")
                  .Append(Enc(c.Def.Desc)).Append("</span></div>");

                sb.Append("<table><thead><tr><th class=\"chk\"></th><th>编号</th><th>项目</th><th>路径</th><th class=\"num\">大小</th><th class=\"num\">文件数</th><th>风险</th><th class=\"note\">说明</th></tr></thead><tbody>");

                foreach (CleanTarget t in items)
                {
                    string p0 = t.Paths.Count > 0 ? t.Paths[0] : "";
                    if (t.Mode == TargetMode.Dism || t.Mode == TargetMode.Shadow) p0 = "（系统级操作，无固定路径）";
                    string more = t.Paths.Count > 1 ? " <span class=\"more\">+" + (t.Paths.Count - 1) + "</span>" : "";

                    string size;
                    if (t.Mode == TargetMode.Dism || t.Mode == TargetMode.Shadow) size = "<span class=\"warn\">按实际</span>";
                    else if (t.Denied) size = "<span class=\"warn\">需管理员</span>";
                    else if (t.Bytes == 0) size = "<span class=\"muted\">0</span>";
                    else size = Fmt.Size(t.Bytes);

                    sb.Append("<tr><td class=\"chk\"><input type=\"checkbox\" data-id=\"").Append(Enc(t.Id))
                      .Append("\" data-bytes=\"").Append(t.Bytes).Append("\"").Append(t.Recommend ? " checked" : "")
                      .Append("></td><td class=\"id\">").Append(Enc(t.Id))
                      .Append("</td><td class=\"nm\">").Append(Enc(t.Label))
                      .Append("</td><td class=\"pth\">").Append(Enc(p0)).Append(more)
                      .Append("</td><td class=\"num\">").Append(size)
                      .Append("</td><td class=\"num\">").Append(Fmt.Count(t.Files))
                      .Append("</td><td><span class=\"tag ").Append(RiskClass(t.Risk)).Append("\">")
                      .Append(Enc(RiskText.Of(t.Risk))).Append("</span></td><td class=\"note\">")
                      .Append(Enc(t.Note)).Append("</td></tr>");
                }
                sb.Append("</tbody></table>");
            }
            return sb.ToString();
        }

        private static string ReportOnly(ScanResult r)
        {
            List<CleanTarget> items = new List<CleanTarget>();
            foreach (CleanTarget t in r.Targets)
                if (t.Exists && !t.Deletable) items.Add(t);
            if (items.Count == 0) return "";
            items.Sort(delegate(CleanTarget a, CleanTarget b) { return b.Bytes.CompareTo(a.Bytes); });

            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"card\"><h2>仅统计 / 不建议直接删除</h2>")
              .Append("<div class=\"lead\">这些是个人数据或大型软件本体。删掉会丢数据，正确做法是迁移或使用软件自带的清理功能。</div>")
              .Append("<table><thead><tr><th>编号</th><th>项目</th><th>路径</th><th class=\"num\">大小</th><th>说明</th></tr></thead><tbody>");
            foreach (CleanTarget t in items)
            {
                string p0 = t.Paths.Count > 0 ? t.Paths[0] : "";
                sb.Append("<tr><td class=\"id\">").Append(Enc(t.Id))
                  .Append("</td><td class=\"nm\">").Append(Enc(t.Label))
                  .Append("</td><td class=\"pth\">").Append(Enc(p0))
                  .Append("</td><td class=\"num\">").Append(Fmt.Size(t.Bytes))
                  .Append("</td><td class=\"note\">").Append(Enc(t.Note)).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            return sb.ToString();
        }

        private static string TopDirs(ScanResult r)
        {
            if (r.TopDirs.Count == 0) return "";
            long max = 1;
            foreach (DirItem d in r.TopDirs) if (d.Bytes > max) max = d.Bytes;

            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"card\"><h2>目录占用排行</h2>")
              .Append("<div class=\"lead\">按目录层级 1-4 聚合，已包含子目录。数值含硬链接重复计算，仅用于定位大户。</div>")
              .Append("<table><thead><tr><th>目录</th><th class=\"num\">大小</th><th></th></tr></thead><tbody>");
            foreach (DirItem d in r.TopDirs)
            {
                double w = d.Bytes * 100.0 / max;
                sb.Append("<tr><td class=\"pth\">").Append(Enc(d.Path))
                  .Append("</td><td class=\"num\">").Append(Fmt.Size(d.Bytes))
                  .Append("</td><td><div class=\"bar\"><i style=\"width:").Append(w.ToString("0.0"))
                  .Append("%\"></i></div></td></tr>");
            }
            sb.Append("</tbody></table></div>");
            return sb.ToString();
        }

        private static string TopFiles(ScanResult r)
        {
            if (r.TopFiles.Count == 0) return "";
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"card\"><h2>最大的文件</h2>")
              .Append("<div class=\"lead\">超过 50 MB 的文件按大小排序，供你判断哪些可以搬到 D 盘。</div>")
              .Append("<table><thead><tr><th>文件</th><th class=\"num\">大小</th><th>修改时间</th></tr></thead><tbody>");
            int n = 0;
            foreach (FileItem f in r.TopFiles)
            {
                if (n >= 40) break;
                n++;
                sb.Append("<tr><td class=\"pth\">").Append(Enc(f.Path))
                  .Append("</td><td class=\"num\">").Append(Fmt.Size(f.Bytes))
                  .Append("</td><td>").Append(f.Modified.ToString("yyyy-MM-dd")).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            return sb.ToString();
        }

        private static string Suggestions(ScanResult r)
        {
            if (r.Suggestions.Count == 0) return "";
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"card\"><h2>清理建议</h2>");
            foreach (Suggestion s in r.Suggestions)
            {
                sb.Append("<div class=\"sugg\"><h3>").Append(Enc(s.Title))
                  .Append("</h3><p>").Append(Enc(s.Body)).Append("</p></div>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Notes(ScanResult r)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"card\"><h2>扫描说明</h2><ul class=\"note-list\">");
            sb.Append("<li>扫描范围：C:\\ 全盘，共 ").Append(Fmt.Count(r.TotalFiles)).Append(" 个文件、")
              .Append(Fmt.Count(r.DirCount)).Append(" 个目录，合计 ").Append(Fmt.Size(r.TotalBytes)).Append("。</li>");
            sb.Append("<li>文件大小合计按硬链接逐个计数，WinSxS、DriverStore 等目录会高于实际磁盘占用。</li>");
            sb.Append("<li>因权限无法读取的目录：").Append(Fmt.Count(r.DeniedCount)).Append(" 个。")
              .Append(r.DeniedCount > 0 ? "以管理员身份运行可补全。" : "").Append("</li>");
            sb.Append("<li>删除为永久删除，不进回收站；执行前必须输入 DELETE 确认，且每一条路径都会经过白名单校验。</li>");
            sb.Append("<li>用户数据（聊天记录、下载、文档、游戏本体）只统计不删除，避免不可逆的数据丢失。</li>");
            sb.Append("</ul>");
            if (r.DeniedPaths.Count > 0)
            {
                sb.Append("<details style=\"margin-top:10px\"><summary style=\"cursor:pointer;color:#64748b;font-size:12px\">查看无权限目录（前 15 个）</summary><ul class=\"note-list\">");
                int k = 0;
                foreach (string d in r.DeniedPaths)
                {
                    if (k >= 15) break;
                    k++;
                    sb.Append("<li>").Append(Enc(d)).Append("</li>");
                }
                sb.Append("</ul></details>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Footer(ScanResult r)
        {
            return Enc("C盘清理助手 v2.0.1 · " + r.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss") + " · 清理前请确认清单");
        }
    }
}
