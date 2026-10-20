using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CCleaner
{
    public class StatsPage : PageBase
    {
        private DataGridView _grid;
        private ScanResult _scan;

        public StatsPage(MainForm host) : base(host)
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(Theme.S(10), Theme.S(8), Theme.S(10), Theme.S(10));

            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            Theme.StyleGrid(_grid);
            _grid.RowTemplate.Height = Theme.S(36);

            AddCol("编号", Theme.S(78), DataGridViewAutoSizeColumnMode.None);
            AddCol("项目", Theme.S(120), DataGridViewAutoSizeColumnMode.None);
            DataGridViewTextBoxColumn path = new DataGridViewTextBoxColumn();
            path.HeaderText = "路径";
            path.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            path.FillWeight = 100f;
            path.ReadOnly = true;
            path.SortMode = DataGridViewColumnSortMode.NotSortable;
            path.DefaultCellStyle.ForeColor = Theme.Muted;
            path.DefaultCellStyle.Font = Theme.F(8.5f);
            _grid.Columns.Add(path);
            AddCol("大小", Theme.S(86), DataGridViewAutoSizeColumnMode.None);
            AddCol("说明", Theme.S(210), DataGridViewAutoSizeColumnMode.None);

            _grid.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            card.Controls.Add(_grid);
            Controls.Add(card);
        }

        private void AddCol(string header, int width, DataGridViewAutoSizeColumnMode mode)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = header;
            c.Width = width;
            c.AutoSizeMode = mode;
            c.ReadOnly = true;
            c.SortMode = DataGridViewColumnSortMode.NotSortable;
            _grid.Columns.Add(c);
        }

        public override void OnScan(ScanResult r)
        {
            _scan = r;
            _grid.Rows.Clear();
            if (_scan == null) return;

            List<CleanTarget> items = _scan.ReportOnlyItems();
            items.Sort(delegate(CleanTarget a, CleanTarget b) { return b.Bytes.CompareTo(a.Bytes); });
            foreach (CleanTarget t in items)
            {
                int i = _grid.Rows.Add();
                DataGridViewRow row = _grid.Rows[i];
                row.Cells[0].Value = t.Id;
                row.Cells[1].Value = t.Label;
                row.Cells[2].Value = t.Paths.Count > 0 ? t.Paths[0] : "";
                row.Cells[3].Value = Fmt.Size(t.Bytes);
                row.Cells[4].Value = t.Note;
            }
        }

        public void ClearAll()
        {
            _scan = null;
            _grid.Rows.Clear();
        }

        public override void OnBusyChanged(bool busy)
        {
            _grid.Enabled = !busy;
        }
    }
}
