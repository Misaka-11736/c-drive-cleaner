using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CCleaner
{
    public class SuggestionPage : PageBase
    {
        private FlowLayoutPanel _flow;
        private ScanResult _scan;
        private bool _laying;

        public SuggestionPage(MainForm host) : base(host)
        {
            _flow = new FlowLayoutPanel();
            _flow.Dock = DockStyle.Fill;
            _flow.AutoScroll = true;
            _flow.FlowDirection = FlowDirection.TopDown;
            _flow.WrapContents = false;
            _flow.BackColor = Theme.Bg;
            _flow.Padding = new Padding(0, 0, Theme.S(8), 0);
            Controls.Add(_flow);

            Resize += delegate { Relayout(); };
            _flow.Resize += delegate { Relayout(); };
            _flow.ControlAdded += delegate { Relayout(); };
        }

        public override void OnScan(ScanResult r)
        {
            _scan = r;
            Rebuild();
        }

        private void Rebuild()
        {
            _flow.SuspendLayout();
            _flow.Controls.Clear();

            if (_scan == null)
            {
                Label hint = new Label();
                hint.Text = "扫描完成后，这里会给出针对这台机器的清理建议。";
                hint.ForeColor = Theme.Muted;
                hint.Font = Theme.F(9f);
                hint.AutoSize = true;
                _flow.Controls.Add(hint);
            }
            else
            {
                foreach (Suggestion s in _scan.Suggestions)
                {
                    SuggestionCard card = new SuggestionCard();
                    card.SetData(s.Title, s.Body);
                    _flow.Controls.Add(card);
                }
            }

            _flow.ResumeLayout();
            Relayout();
        }

        public void ClearAll()
        {
            _scan = null;
            Rebuild();
        }

        private void Relayout()
        {
            if (_laying) return;
            _laying = true;
            try
            {
                int w = _flow.ClientSize.Width - Theme.S(26);
                if (w < Theme.S(320)) w = Theme.S(320);
                foreach (Control c in _flow.Controls)
                {
                    SuggestionCard card = c as SuggestionCard;
                    if (card == null) continue;
                    if (card.Width != w) card.Width = w;
                    int h = card.MeasureHeight(w);
                    if (card.Height != h) card.Height = h;
                }
            }
            finally
            {
                _laying = false;
            }
        }
    }

    public class SuggestionCard : Control
    {
        private string _title = "";
        private string _body = "";

        public SuggestionCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Margin = new Padding(0, 0, 0, Theme.S(10));
        }

        public void SetData(string title, string body)
        {
            _title = title;
            _body = body;
            Invalidate();
        }

        public int MeasureHeight(int width)
        {
            Size sz = TextRenderer.MeasureText(_body, Theme.F(9f),
                new Size(Math.Max(Theme.S(100), width - Theme.S(46)), int.MaxValue),
                TextFormatFlags.WordBreak);
            return Theme.S(16) + Theme.S(22) + Theme.S(4) + sz.Height + Theme.S(16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, r, Theme.S(10), Color.White);
            Theme.DrawRound(g, r, Theme.S(10), Theme.Border, 1f);

            using (SolidBrush b = new SolidBrush(Theme.Amber))
                g.FillRectangle(b, new Rectangle(Theme.S(14), Theme.S(14), Theme.S(3), Height - Theme.S(28)));

            TextRenderer.DrawText(g, _title, Theme.F(10.5f, FontStyle.Bold),
                new Rectangle(Theme.S(26), Theme.S(12), Width - Theme.S(40), Theme.S(24)),
                Color.FromArgb(146, 64, 14), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, _body, Theme.F(9f),
                new Rectangle(Theme.S(26), Theme.S(40), Width - Theme.S(42), Height - Theme.S(50)),
                Theme.Muted, TextFormatFlags.Left | TextFormatFlags.WordBreak);
        }
    }
}
