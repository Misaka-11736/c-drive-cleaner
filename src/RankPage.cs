using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CCleaner
{
    public class RankPage : PageBase
    {
        private DataGridView _dirGrid;
        private DataGridView _fileGrid;
        private ScanResult _scan;

        public RankPage(MainForm host) : base(host)
        {
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.BackColor = Theme.Bg;
            table.ColumnCount = 1;
            table.RowCount = 2;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 60f));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 40f));

            _dirGrid = BuildGrid();
            _dirGrid.Columns.Add(TextCol("目录", 0, true, DataGridViewAutoSizeColumnMode.Fill));
            _dirGrid.Columns.Add(TextCol("大小", Theme.S(92), false, DataGridViewAutoSizeColumnMode.None));
            _dirGrid.Columns.Add(TextCol("占比", Theme.S(140), false, DataGridViewAutoSizeColumnMode.None));
            _dirGrid.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _dirGrid.CellPainting += DirCellPainting;

            _fileGrid = BuildGrid();
            _fileGrid.Columns.Add(TextCol("文件", 0, true, DataGridViewAutoSizeColumnMode.Fill));
            _fileGrid.Columns.Add(TextCol("大小", Theme.S(92), false, DataGridViewAutoSizeColumnMode.None));
            _fileGrid.Columns.Add(TextCol("修改时间", Theme.S(92), false, DataGridViewAutoSizeColumnMode.None));
            _fileGrid.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            table.Controls.Add(Wrap("目录占用排行", "按层级 1-4 聚合，含子目录；数值含硬链接重复计算", _dirGrid), 0, 0);
            table.Controls.Add(Wrap("最大的文件", "超过 50 MB，按大小排序", _fileGrid), 0, 1);
            Controls.Add(table);
        }

        private DataGridView BuildGrid()
        {
            DataGridView g = new DataGridView();
            g.Dock = DockStyle.Fill;
            Theme.StyleGrid(g);
            g.RowTemplate.Height = Theme.S(32);
            return g;
        }

        private static DataGridViewTextBoxColumn TextCol(string header, int width, bool readOnly, DataGridViewAutoSizeColumnMode mode)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = header;
            if (width > 0) c.Width = width;
            c.AutoSizeMode = mode;
            c.ReadOnly = readOnly;
            c.SortMode = DataGridViewColumnSortMode.NotSortable;
            return c;
        }

        private Control Wrap(string title, string subtitle, DataGridView grid)
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, 0, Theme.S(10));
            card.Padding = new Padding(Theme.S(14), Theme.S(10), Theme.S(14), Theme.S(10));

            Label t = new Label();
            t.Text = title;
            t.Font = Theme.F(10.5f, FontStyle.Bold);
            t.ForeColor = Theme.Text;
            t.AutoSize = false;
            t.Height = Theme.S(24);
            t.Dock = DockStyle.Top;
            t.BackColor = Color.White;

            Label s = new Label();
            s.Text = subtitle;
            s.Font = Theme.F(8f);
            s.ForeColor = Theme.Subtle;
            s.AutoSize = false;
            s.Height = Theme.S(20);
            s.Dock = DockStyle.Top;
            s.BackColor = Color.White;

            card.Controls.Add(grid);
            card.Controls.Add(s);
            card.Controls.Add(t);
            return card;
        }

        public override void OnScan(ScanResult r)
        {
            _scan = r;
            _dirGrid.Rows.Clear();
            _fileGrid.Rows.Clear();
            if (_scan == null) return;

            long max = 1;
            foreach (DirItem d in _scan.TopDirs) if (d.Bytes > max) max = d.Bytes;

            foreach (DirItem d in _scan.TopDirs)
            {
                int i = _dirGrid.Rows.Add();
                DataGridViewRow row = _dirGrid.Rows[i];
                row.Cells[0].Value = d.Path;
                row.Cells[1].Value = Fmt.Size(d.Bytes);
                row.Cells[2].Value = d.Bytes / (double)max;
                row.Cells[0].Style.ForeColor = Theme.Muted;
                row.Cells[0].Style.Font = Theme.F(8.5f);
            }

            int n = 0;
            foreach (FileItem f in _scan.TopFiles)
            {
                if (n >= 60) break;
                n++;
                int i = _fileGrid.Rows.Add();
                DataGridViewRow row = _fileGrid.Rows[i];
                row.Cells[0].Value = f.Path;
                row.Cells[1].Value = Fmt.Size(f.Bytes);
                row.Cells[2].Value = f.Modified.ToString("yyyy-MM-dd");
                row.Cells[0].Style.ForeColor = Theme.Muted;
                row.Cells[0].Style.Font = Theme.F(8.5f);
            }
        }

        public void ClearAll()
        {
            _scan = null;
            _dirGrid.Rows.Clear();
            _fileGrid.Rows.Clear();
        }

        public override void OnBusyChanged(bool busy)
        {
            _dirGrid.Enabled = !busy;
            _fileGrid.Enabled = !busy;
        }

        private void DirCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 2) return;
            object v = _dirGrid.Rows[e.RowIndex].Cells[2].Value;
            if (v == null) return;
            double ratio = Convert.ToDouble(v);

            e.PaintBackground(e.CellBounds, true);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int width = e.CellBounds.Width - Theme.S(12);
            int x = e.CellBounds.X + Theme.S(6);
            int y = e.CellBounds.Y + (e.CellBounds.Height - Theme.S(8)) / 2;
            Rectangle track = new Rectangle(x, y, width, Theme.S(8));
            Theme.FillRound(g, track, Theme.S(4), Color.FromArgb(238, 242, 247));
            int fw = (int)(width * Math.Max(0, Math.Min(1, ratio)));
            if (fw > 2)
                Theme.FillRound(g, new Rectangle(x, y, fw, Theme.S(8)), Theme.S(4), Color.FromArgb(59, 130, 246));

            e.Handled = true;
        }
    }
}
