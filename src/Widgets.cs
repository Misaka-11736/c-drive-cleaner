using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CCleaner
{
    public class NavItem : Control
    {
        public int Index;
        public bool Selected;
        private bool _hover;

        public event EventHandler Activate;

        public NavItem()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(46);
            Cursor = Cursors.Hand;
            BackColor = Theme.Sidebar;
            Font = Theme.F(10f);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (Activate != null) Activate(this, e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = ClientRectangle;

            if (Selected)
            {
                using (SolidBrush b = new SolidBrush(Theme.Sidebar2)) g.FillRectangle(b, r);
                using (SolidBrush b = new SolidBrush(Theme.Primary))
                    g.FillRectangle(b, new Rectangle(0, Theme.S(8), Theme.S(3), r.Height - Theme.S(16)));
            }
            else if (_hover)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(27, 41, 66))) g.FillRectangle(b, r);
            }

            Color fore = Selected ? Color.White : Theme.SidebarText;
            Rectangle tr = new Rectangle(Theme.S(22), 0, r.Width - Theme.S(22), r.Height);
            TextRenderer.DrawText(g, Text, Font, tr, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    public class RoundButton : Control
    {
        public Color Fill = Color.White;
        public Color HoverFill = Color.FromArgb(241, 245, 249);
        public Color TextColor = Theme.Text;
        public Color BorderColor = Theme.Border;
        public int Radius = 8;

        private bool _hover;

        public RoundButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(34);
            Cursor = Cursors.Hand;
            Font = Theme.F(9f);
            BackColor = Theme.Bg;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            Color fill = Enabled ? (_hover ? HoverFill : Fill) : Color.FromArgb(241, 245, 249);
            Theme.FillRound(g, r, Theme.S(Radius), fill);
            if (BorderColor != Color.Transparent)
                Theme.DrawRound(g, r, Theme.S(Radius), BorderColor, 1f);

            Color fore = Enabled ? TextColor : Theme.Subtle;
            TextRenderer.DrawText(g, Text, Font, r, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    public class SlimProgress : Control
    {
        private int _value;
        private readonly Timer _timer;
        private int _offset;

        public SlimProgress()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(8);
            BackColor = Theme.Bg;
            _timer = new Timer();
            _timer.Interval = 33;
            _timer.Tick += delegate
            {
                _offset += Theme.S(6);
                if (_offset > Width) _offset = -Theme.S(60);
                Invalidate();
            };
        }

        /// <summary>-1 表示不确定进度（滚动），0-100 为确定进度。</summary>
        public int Value
        {
            get { return _value; }
            set
            {
                _value = value;
                if (value < 0)
                {
                    if (!_timer.Enabled) _timer.Start();
                }
                else
                {
                    _timer.Stop();
                }
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width, Height);
            Theme.FillRound(g, r, Height / 2, Color.FromArgb(226, 232, 240));

            if (_value < 0)
            {
                int w = Math.Max(Theme.S(50), Width / 5);
                int x = _offset;
                Rectangle mr = new Rectangle(x, 0, w, Height);
                using (GraphicsPath p = Theme.RoundRect(mr, Height / 2))
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)),
                    Color.FromArgb(147, 197, 253), Theme.Primary, 0f))
                {
                    g.SetClip(new Rectangle(0, 0, Width, Height));
                    g.FillPath(b, p);
                    g.ResetClip();
                }
            }
            else
            {
                int w = (int)(Width * Math.Max(0, Math.Min(100, _value)) / 100.0);
                if (w > 1)
                {
                    Rectangle fr = new Rectangle(0, 0, w, Height);
                    using (GraphicsPath p = Theme.RoundRect(fr, Height / 2))
                    using (LinearGradientBrush b = new LinearGradientBrush(
                        new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)),
                        Color.FromArgb(59, 130, 246), Theme.Primary, 0f))
                        g.FillPath(b, p);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }

    public class Chip : Control
    {
        public bool Active;
        public Color Accent = Theme.Primary;

        public Chip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(30);
            Cursor = Cursors.Hand;
            Font = Theme.F(9f);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (Active)
            {
                Theme.FillRound(g, r, Height / 2, Accent);
                TextRenderer.DrawText(g, Text, Font, r, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                Theme.FillRound(g, r, Height / 2, Color.White);
                Theme.DrawRound(g, r, Height / 2, Theme.Border, 1f);
                TextRenderer.DrawText(g, Text, Font, r, Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
        }
    }
}
