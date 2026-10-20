using System;
using System.Drawing;
using System.Windows.Forms;

namespace CCleaner
{
    public class UpdateBanner : Panel
    {
        private Label _text;
        private RoundButton _view;
        private RoundButton _ignore;
        private RoundButton _close;

        public event EventHandler ViewClicked;
        public event EventHandler Ignored;

        public UpdateBanner()
        {
            Dock = DockStyle.Top;
            Height = Theme.S(42);
            BackColor = Color.FromArgb(239, 246, 255);
            Visible = false;

            _text = new Label();
            _text.AutoSize = false;
            _text.BackColor = Color.Transparent;
            _text.ForeColor = Color.FromArgb(30, 64, 175);
            _text.Font = Theme.F(9f);
            _text.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(_text);

            _view = SmallButton("查看", Theme.Primary, Color.White);
            _view.Click += delegate { if (ViewClicked != null) ViewClicked(this, EventArgs.Empty); };
            Controls.Add(_view);

            _ignore = SmallButton("忽略此版本", Color.White, Theme.Muted);
            _ignore.Width = Theme.S(92);
            _ignore.Click += delegate
            {
                if (Ignored != null) Ignored(this, EventArgs.Empty);
                Visible = false;
            };
            Controls.Add(_ignore);

            _close = SmallButton("×", Color.Transparent, Theme.Subtle);
            _close.Width = Theme.S(26);
            _close.Click += delegate { Visible = false; };
            Controls.Add(_close);

            Resize += delegate { LayoutChildren(); };
            LayoutChildren();
        }

        private RoundButton SmallButton(string text, Color fill, Color fore)
        {
            RoundButton b = new RoundButton();
            b.Text = text;
            b.Width = Theme.S(62);
            b.Height = Theme.S(26);
            b.Font = Theme.F(8.5f);
            b.Fill = fill;
            b.HoverFill = fill == Color.Transparent ? Color.FromArgb(219, 234, 254) : Color.FromArgb(191, 219, 254);
            b.TextColor = fore;
            b.BorderColor = fill == Color.Transparent ? Color.Transparent : Color.FromArgb(191, 219, 254);
            return b;
        }

        public void ShowUpdate(string version, string current)
        {
            _text.Text = "发现新版本 " + version + "（当前 " + current + "）· 建议更新后再清理";
            Visible = true;
            LayoutChildren();
        }

        private void LayoutChildren()
        {
            if (_text == null) return;

            int y = (Height - Theme.S(26)) / 2;
            int right = Width - Theme.S(12);

            _close.Location = new Point(right - _close.Width, y);
            right -= _close.Width + Theme.S(6);
            _ignore.Location = new Point(right - _ignore.Width, y);
            right -= _ignore.Width + Theme.S(8);
            _view.Location = new Point(right - _view.Width, y);

            _text.Location = new Point(Theme.S(16), 0);
            _text.Size = new Size(Math.Max(Theme.S(120), _view.Left - Theme.S(24)), Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Color.FromArgb(191, 219, 254)))
                e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }
    }
}
