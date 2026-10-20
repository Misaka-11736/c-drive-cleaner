using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace CCleaner
{
    public static class Theme
    {
        public static float DpiScale = 1f;

        public static int S(float v)
        {
            return (int)Math.Round(v * DpiScale);
        }

        public static readonly Color Bg = Color.FromArgb(238, 242, 247);
        public static readonly Color Card = Color.White;
        public static readonly Color Text = Color.FromArgb(30, 41, 59);
        public static readonly Color Muted = Color.FromArgb(100, 116, 139);
        public static readonly Color Subtle = Color.FromArgb(148, 163, 184);
        public static readonly Color Border = Color.FromArgb(226, 232, 240);
        public static readonly Color LightBorder = Color.FromArgb(238, 242, 247);
        public static readonly Color Primary = Color.FromArgb(29, 78, 216);
        public static readonly Color PrimaryDark = Color.FromArgb(30, 64, 175);
        public static readonly Color PrimaryLight = Color.FromArgb(239, 246, 255);
        public static readonly Color Sidebar = Color.FromArgb(15, 23, 42);
        public static readonly Color Sidebar2 = Color.FromArgb(22, 33, 54);
        public static readonly Color SidebarText = Color.FromArgb(203, 213, 225);
        public static readonly Color Green = Color.FromArgb(15, 123, 69);
        public static readonly Color GreenBack = Color.FromArgb(232, 247, 238);
        public static readonly Color Amber = Color.FromArgb(245, 158, 11);
        public static readonly Color Red = Color.FromArgb(180, 35, 24);

        private static readonly Dictionary<string, Font> FontCache = new Dictionary<string, Font>();

        public static Font F(float size)
        {
            return F(size, FontStyle.Regular);
        }

        public static Font F(float size, FontStyle style)
        {
            string key = size.ToString("0.##") + "|" + (int)style;
            Font f;
            if (FontCache.TryGetValue(key, out f)) return f;
            try { f = new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point); }
            catch
            {
                try { f = new Font("Microsoft YaHei", size, style, GraphicsUnit.Point); }
                catch { f = new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point); }
            }
            FontCache[key] = f;
            return f;
        }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 0)
            {
                p.AddRectangle(r);
                return p;
            }
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color color)
        {
            using (GraphicsPath p = RoundRect(r, radius))
            using (SolidBrush b = new SolidBrush(color))
                g.FillPath(b, p);
        }

        public static void DrawRound(Graphics g, Rectangle r, int radius, Color color, float width)
        {
            using (GraphicsPath p = RoundRect(r, radius))
            using (Pen pen = new Pen(color, width))
                g.DrawPath(pen, p);
        }

        public static void DrawBadge(Graphics g, string text, int x, int y, Color back, Color fore, Font font)
        {
            SizeF sz = g.MeasureString(text, font);
            int w = (int)sz.Width + S(16);
            int h = (int)sz.Height + S(2);
            Rectangle r = new Rectangle(x, y, w, h);
            FillRound(g, r, h / 2, back);
            using (SolidBrush b = new SolidBrush(fore))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(text, font, b, r, sf);
                sf.Dispose();
            }
            return;
        }

        public static int BadgeWidth(Graphics g, string text, Font font)
        {
            SizeF sz = g.MeasureString(text, font);
            return (int)sz.Width + S(16);
        }

        public static void StyleGrid(DataGridView g)
        {
            g.BackgroundColor = Color.White;
            g.BorderStyle = BorderStyle.None;
            g.GridColor = LightBorder;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.White;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
            g.ColumnHeadersDefaultCellStyle.Font = F(8.5f, FontStyle.Bold);
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(S(6), 0, S(6), 0);
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.White;
            g.ColumnHeadersHeight = S(34);
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AllowUserToOrderColumns = false;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.DefaultCellStyle.BackColor = Color.White;
            g.DefaultCellStyle.ForeColor = Text;
            g.DefaultCellStyle.SelectionBackColor = PrimaryLight;
            g.DefaultCellStyle.SelectionForeColor = Text;
            g.DefaultCellStyle.Font = F(9f);
            g.DefaultCellStyle.Padding = new Padding(S(6), 0, S(6), 0);
            g.RowTemplate.Height = S(34);
            g.ShowCellToolTips = true;
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 253);
            g.ScrollBars = ScrollBars.Vertical;
        }

        public static void DrawBar(Graphics g, Rectangle r, double ratio, Color color)
        {
            FillRound(g, r, r.Height / 2, Color.FromArgb(238, 242, 247));
            int w = (int)(r.Width * Math.Max(0, Math.Min(1, ratio)));
            if (w < 2) w = 2;
            Rectangle fill = new Rectangle(r.X, r.Y, w, r.Height);
            FillRound(g, fill, r.Height / 2, color);
        }
    }

    /// <summary>圆角白卡片。</summary>
    public class CardPanel : Panel
    {
        private int _radius = 12;

        public int Radius
        {
            get { return _radius; }
            set { _radius = value; Invalidate(); }
        }

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Padding = new Padding(Theme.S(18));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(e.Graphics, r, _radius, Color.White);
            Theme.DrawRound(e.Graphics, r, _radius, Theme.Border, 1f);
            base.OnPaint(e);
        }
    }

    public class GradientPanel : Panel
    {
        public Color Color1 = Color.FromArgb(15, 42, 92);
        public Color Color2 = Color.FromArgb(59, 130, 246);
        public int Radius = 14;

        public GradientPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width, Height);
            using (GraphicsPath p = Theme.RoundRect(r, Radius))
            using (LinearGradientBrush b = new LinearGradientBrush(r, Color1, Color2, 20f))
            {
                e.Graphics.FillPath(b, p);
            }
            base.OnPaint(e);
        }
    }
}
