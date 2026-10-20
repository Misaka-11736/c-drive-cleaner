using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CCleaner
{
    public class OverviewPage : PageBase
    {
        private ScanResult _scan;

        public OverviewPage(MainForm host) : base(host)
        {
            AutoScroll = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            UpdateScroll();
        }

        public override void OnScan(ScanResult r)
        {
            _scan = r;
            UpdateScroll();
            Invalidate();
        }

        public override void OnBusyChanged(bool busy)
        {
            Invalidate();
        }

        private int CategoryRowCount()
        {
            if (_scan == null) return 0;
            int n = 0;
            foreach (CategoryResult c in _scan.Categories) if (c.Items.Count > 0) n++;
            return n;
        }

        private void UpdateScroll()
        {
            int h;
            if (_scan == null) h = 360;
            else h = 422 + CategoryRowCount() * 46;
            AutoScrollMinSize = new Size(0, Theme.S(h));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScroll();
        }

        private static void Card(Graphics g, Rectangle r)
        {
            Theme.FillRound(g, r, Theme.S(12), Color.White);
            Theme.DrawRound(g, r, Theme.S(12), Theme.Border, 1f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

            int w = ClientSize.Width - (VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0);
            if (w < Theme.S(400)) w = Theme.S(400);

            // ---------------- Hero ----------------
            Rectangle hero = new Rectangle(0, 0, w, Theme.S(100));
            using (GraphicsPath p = Theme.RoundRect(hero, Theme.S(14)))
            using (LinearGradientBrush b = new LinearGradientBrush(hero,
                Color.FromArgb(15, 42, 92), Color.FromArgb(59, 130, 246), 18f))
            {
                g.FillPath(b, p);
            }

            TextRenderer.DrawText(g, "C盘空间分析与清理清单", Theme.F(17f, FontStyle.Bold),
                new Rectangle(Theme.S(28), Theme.S(16), w - Theme.S(56), Theme.S(36)), Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            string meta;
            if (_scan == null)
            {
                meta = "尚未扫描 · 点击右上角「重新扫描」开始分析";
            }
            else
            {
                meta = string.Format("{0} · {1} · 扫描于 {2} · 用时 {3:0.0} 分钟 · {4}",
                    _scan.Computer, _scan.User, _scan.GeneratedAt.ToString("yyyy-MM-dd HH:mm"),
                    _scan.DurationSec / 60.0, _scan.Elevated ? "管理员" : "普通用户");
            }
            TextRenderer.DrawText(g, meta, Theme.F(8.5f),
                new Rectangle(Theme.S(29), Theme.S(56), w - Theme.S(58), Theme.S(24)),
                Color.FromArgb(199, 210, 254), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            if (_scan == null)
            {
                Rectangle empty = new Rectangle(0, Theme.S(116), w, Theme.S(210));
                Card(g, empty);
                TextRenderer.DrawText(g, "还没有扫描结果", Theme.F(13f, FontStyle.Bold),
                    new Rectangle(Theme.S(24), Theme.S(140), w - Theme.S(48), Theme.S(30)),
                    Theme.Text, TextFormatFlags.Left);
                TextRenderer.DrawText(g,
                    "点击右上角「重新扫描」，程序会遍历 C 盘（只读），统计每个目录、每类垃圾的占用，并生成可勾选的清理清单。\r\n扫描过程中可以随时点「取消」。",
                    Theme.F(9f), new Rectangle(Theme.S(24), Theme.S(178), w - Theme.S(48), Theme.S(80)),
                    Theme.Muted, TextFormatFlags.Left | TextFormatFlags.WordBreak);
                return;
            }

            // ---------------- 磁盘 ----------------
            int y = Theme.S(116);
            Rectangle disk = new Rectangle(0, y, w, Theme.S(126));
            Card(g, disk);
            TextRenderer.DrawText(g, "C: 磁盘使用", Theme.F(11f, FontStyle.Bold),
                new Rectangle(Theme.S(20), y + Theme.S(14), Theme.S(240), Theme.S(24)), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            double usedPct = _scan.DriveTotal > 0 ? (_scan.DriveTotal - _scan.DriveFree) * 100.0 / _scan.DriveTotal : 0;
            string rightText = "可用 " + Fmt.Size(_scan.DriveFree);
            TextRenderer.DrawText(g, rightText, Theme.F(9f),
                new Rectangle(w - Theme.S(240), y + Theme.S(14), Theme.S(220), Theme.S(24)), Theme.Text,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, "总容量 " + Fmt.Size(_scan.DriveTotal) + " · 已用 " + Fmt.Size(_scan.DriveUsed),
                Theme.F(8.5f), new Rectangle(Theme.S(20), y + Theme.S(38), Theme.S(420), Theme.S(20)),
                Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            Rectangle bar = new Rectangle(Theme.S(20), y + Theme.S(64), w - Theme.S(40), Theme.S(14));
            Theme.DrawBar(g, bar, usedPct / 100.0, usedPct >= 85 ? Theme.Red : Theme.Primary);

            string legend = string.Format("已用 {0:0.0}%      可用 {1:0.0}%      本次可清理 {2}",
                usedPct, 100 - usedPct, Fmt.Size(_scan.CleanBytes));
            TextRenderer.DrawText(g, legend, Theme.F(8.5f),
                new Rectangle(Theme.S(20), y + Theme.S(86), w - Theme.S(40), Theme.S(24)),
                Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            // ---------------- KPI ----------------
            y += Theme.S(142);
            int gap = Theme.S(14);
            int kw = (w - gap * 3) / 4;
            DrawKpi(g, new Rectangle(0, y, kw, Theme.S(86)), "可清理合计", Fmt.Size(_scan.CleanBytes), Theme.Primary);
            DrawKpi(g, new Rectangle(kw + gap, y, kw, Theme.S(86)), "其中推荐项", Fmt.Size(_scan.RecBytes), Theme.Green);
            DrawKpi(g, new Rectangle((kw + gap) * 2, y, kw, Theme.S(86)), "仅统计不建议删", Fmt.Size(_scan.ReportBytes), Color.FromArgb(71, 85, 105));
            DrawKpi(g, new Rectangle((kw + gap) * 3, y, kw, Theme.S(86)), "扫描文件 / 目录",
                Fmt.ShortCount(_scan.TotalFiles) + " / " + Fmt.ShortCount(_scan.DirCount), Theme.Text);

            // ---------------- 分类 ----------------
            y += Theme.S(96);
            int rows = CategoryRowCount();
            Rectangle cats = new Rectangle(0, y, w, Theme.S(42) + rows * Theme.S(46) + Theme.S(14));
            Card(g, cats);
            TextRenderer.DrawText(g, "分类汇总", Theme.F(11f, FontStyle.Bold),
                new Rectangle(Theme.S(20), y + Theme.S(12), Theme.S(240), Theme.S(24)), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "按类别统计可清理空间，详细清单见「可清理」页", Theme.F(8.5f),
                new Rectangle(w - Theme.S(360), y + Theme.S(12), Theme.S(340), Theme.S(24)), Theme.Subtle,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

            int ry = y + Theme.S(42);
            long maxClean = 1;
            foreach (CategoryResult c in _scan.Categories) if (c.CleanBytes > maxClean) maxClean = c.CleanBytes;

            foreach (CategoryResult c in _scan.Categories)
            {
                if (c.Items.Count == 0) continue;
                Rectangle dot = new Rectangle(Theme.S(22), ry + Theme.S(19), Theme.S(8), Theme.S(8));
                using (SolidBrush b = new SolidBrush(c.Def.Color)) g.FillEllipse(b, dot);

                TextRenderer.DrawText(g, c.Def.Name, Theme.F(9.5f),
                    new Rectangle(Theme.S(38), ry, Theme.S(130), Theme.S(46)), Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                int barX = Theme.S(176);
                int barW = w - barX - Theme.S(150);
                if (barW > Theme.S(40))
                {
                    Rectangle br = new Rectangle(barX, ry + Theme.S(19), barW, Theme.S(8));
                    Theme.DrawBar(g, br, c.CleanBytes / (double)maxClean, c.Def.Color);
                }

                string sizeText = c.CleanBytes == 0 ? "按实际" : Fmt.Size(c.CleanBytes);
                if (c.CleanBytes == 0)
                {
                    bool hasDel = false;
                    foreach (CleanTarget t in c.Items) if (t.Deletable) { hasDel = true; break; }
                    if (!hasDel) sizeText = "—";
                }
                TextRenderer.DrawText(g, sizeText, Theme.F(9f),
                    new Rectangle(w - Theme.S(160), ry, Theme.S(120), Theme.S(46)), Theme.Text,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                ry += Theme.S(46);
                using (Pen p = new Pen(Theme.LightBorder))
                    g.DrawLine(p, Theme.S(22), ry, w - Theme.S(22), ry);
            }
        }

        private static void DrawKpi(Graphics g, Rectangle r, string label, string value, Color valueColor)
        {
            Card(g, r);
            TextRenderer.DrawText(g, label, Theme.F(8.5f),
                new Rectangle(r.X + Theme.S(16), r.Y + Theme.S(12), r.Width - Theme.S(32), Theme.S(20)),
                Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, value, Theme.F(15f, FontStyle.Bold),
                new Rectangle(r.X + Theme.S(16), r.Y + Theme.S(36), r.Width - Theme.S(32), Theme.S(34)),
                valueColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }
}
