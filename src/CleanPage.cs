using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CCleaner
{
    public class CleanPage : PageBase
    {
        private DataGridView _grid;
        private ComboBox _filter;
        private RoundButton _btnAll;
        private RoundButton _btnNone;
        private Label _countLabel;
        private Panel _toolbar;
        private CardPanel _card;
        private ScanResult _scan;
        private List<CategoryDef> _cats;
        private bool _suppress;
        private string _catId = "all";

        public CleanPage(MainForm host) : base(host)
        {
            _cats = TargetCatalog.CreateCategories();

            _toolbar = new Panel();
            _toolbar.Dock = DockStyle.Top;
            _toolbar.Height = Theme.S(48);
            _toolbar.BackColor = Theme.Bg;

            _filter = new ComboBox();
            _filter.DropDownStyle = ComboBoxStyle.DropDownList;
            _filter.Font = Theme.F(9f);
            _filter.FlatStyle = FlatStyle.Flat;
            _filter.Width = Theme.S(150);
            _filter.Height = Theme.S(28);
            _filter.Items.Add("全部分类");
            foreach (CategoryDef c in _cats) _filter.Items.Add(c.Name);
            _filter.SelectedIndex = 0;
            _filter.SelectedIndexChanged += delegate
            {
                if (_suppress) return;
                _catId = _filter.SelectedIndex <= 0 ? "all" : _cats[_filter.SelectedIndex - 1].Id;
                Fill();
            };
            _toolbar.Controls.Add(_filter);

            _btnAll = new RoundButton();
            _btnAll.Text = "全选";
            _btnAll.Width = Theme.S(60);
            _btnAll.Height = Theme.S(30);
            _btnAll.Click += delegate { SetAll(true); };
            _toolbar.Controls.Add(_btnAll);

            _btnNone = new RoundButton();
            _btnNone.Text = "全不选";
            _btnNone.Width = Theme.S(68);
            _btnNone.Height = Theme.S(30);
            _btnNone.Click += delegate { SetAll(false); };
            _toolbar.Controls.Add(_btnNone);

            _countLabel = new Label();
            _countLabel.AutoSize = false;
            _countLabel.BackColor = Color.Transparent;
            _countLabel.ForeColor = Theme.Muted;
            _countLabel.Font = Theme.F(8.5f);
            _countLabel.TextAlign = ContentAlignment.MiddleRight;
            _toolbar.Controls.Add(_countLabel);

            _card = new CardPanel();
            _card.Dock = DockStyle.Fill;
            _card.Padding = new Padding(Theme.S(10), Theme.S(8), Theme.S(10), Theme.S(10));
            _card.Margin = new Padding(0);

            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            Theme.StyleGrid(_grid);
            _grid.RowTemplate.Height = Theme.S(36);

            DataGridViewCheckBoxColumn chk = new DataGridViewCheckBoxColumn();
            chk.HeaderText = "";
            chk.Width = Theme.S(34);
            chk.SortMode = DataGridViewColumnSortMode.NotSortable;
            chk.ReadOnly = false;
            _grid.Columns.Add(chk);

            AddTextColumn("编号", Theme.S(84), true);
            AddTextColumn("项目", Theme.S(112), true);
            AddTextColumn("分类", Theme.S(52), true);

            DataGridViewTextBoxColumn path = new DataGridViewTextBoxColumn();
            path.HeaderText = "路径";
            path.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            path.FillWeight = 100f;
            path.SortMode = DataGridViewColumnSortMode.NotSortable;
            path.ReadOnly = true;
            path.DefaultCellStyle.ForeColor = Theme.Muted;
            path.DefaultCellStyle.Font = Theme.F(8.5f);
            _grid.Columns.Add(path);

            AddTextColumn("大小", Theme.S(82), true);
            AddTextColumn("风险", Theme.S(54), true);
            AddTextColumn("说明", Theme.S(170), true);

            _grid.Columns[5].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            _grid.CurrentCellDirtyStateChanged += delegate
            {
                if (_grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.CellValueChanged += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (_suppress || e.RowIndex < 0) return;
                if (e.ColumnIndex == 0)
                {
                    DataGridViewRow row = _grid.Rows[e.RowIndex];
                    CleanTarget t = row.Tag as CleanTarget;
                    if (t != null) t.Selected = Convert.ToBoolean(row.Cells[0].Value);
                    UpdateTotals();
                }
            };
            _grid.CellPainting += GridCellPainting;
            _grid.DataError += delegate { };

            _card.Controls.Add(_grid);

            Controls.Add(_card);
            Controls.Add(_toolbar);

            Resize += delegate { LayoutToolbar(); };
            LayoutToolbar();
        }

        private void AddTextColumn(string header, int width, bool readOnly)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = header;
            c.Width = width;
            c.SortMode = DataGridViewColumnSortMode.NotSortable;
            c.ReadOnly = readOnly;
            c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            _grid.Columns.Add(c);
        }

        private void LayoutToolbar()
        {
            int y = Theme.S(9);
            _filter.Location = new Point(0, y);
            _filter.Width = Theme.S(150);
            _btnAll.Location = new Point(Theme.S(160), y);
            _btnNone.Location = new Point(Theme.S(228), y);
            _countLabel.Location = new Point(Math.Max(Theme.S(310), _toolbar.Width - Theme.S(230)), 0);
            _countLabel.Size = new Size(Theme.S(226), _toolbar.Height);
        }

        public override void OnScan(ScanResult r)
        {
            _scan = r;
            if (_scan != null)
            {
                foreach (CleanTarget t in _scan.Targets) t.Selected = t.Recommend && t.Deletable;
            }
            Fill();
        }

        public override void OnBusyChanged(bool busy)
        {
            _grid.Enabled = !busy;
        }

        public void ClearAll()
        {
            _scan = null;
            _suppress = true;
            _grid.Rows.Clear();
            _suppress = false;
            UpdateTotals();
        }

        public void SelectRecommended()
        {
            if (_scan != null)
            {
                foreach (CleanTarget t in _scan.Targets) t.Selected = t.Recommend && t.Deletable;
            }
            Fill();
        }

        private void SetAll(bool value)
        {
            _suppress = true;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                CleanTarget t = row.Tag as CleanTarget;
                if (t == null) continue;
                t.Selected = value;
                row.Cells[0].Value = value;
            }
            _suppress = false;
            UpdateTotals();
        }

        private void Fill()
        {
            _suppress = true;
            _grid.Rows.Clear();

            if (_scan != null)
            {
                foreach (CategoryResult c in _scan.Categories)
                {
                    if (_catId != "all" && c.Def.Id != _catId) continue;
                    foreach (CleanTarget t in c.Items)
                    {
                        if (!t.Deletable) continue;
                        if (t.Bytes == 0 && !t.Denied && t.Mode != TargetMode.Dism && t.Mode != TargetMode.Shadow) continue;

                        int idx = _grid.Rows.Add();
                        DataGridViewRow row = _grid.Rows[idx];
                        row.Cells[0].Value = t.Selected;
                        row.Cells[1].Value = t.Id;
                        row.Cells[2].Value = t.Label;
                        row.Cells[3].Value = t.Cat;
                        row.Cells[4].Value = t.Paths.Count > 0 ? t.Paths[0] : "（系统级操作，无固定路径）";
                        row.Cells[5].Value = SizeText(t);
                        row.Cells[6].Value = t.Risk;
                        row.Cells[7].Value = t.Note;
                        row.Tag = t;
                    }
                }
            }

            _suppress = false;
            UpdateTotals();
        }

        private static string SizeText(CleanTarget t)
        {
            if (t.Mode == TargetMode.Dism || t.Mode == TargetMode.Shadow) return "按实际";
            if (t.Denied) return "需管理员";
            if (t.Bytes == 0) return "0";
            return Fmt.Size(t.Bytes);
        }

        private void UpdateTotals()
        {
            int n = 0;
            long bytes = 0;
            if (_scan != null)
            {
                foreach (CleanTarget t in _scan.Targets)
                {
                    if (!t.Exists || !t.Selected || !t.Deletable) continue;
                    n++;
                    bytes += t.Bytes;
                }
            }
            _countLabel.Text = string.Format("当前已选 {0} 项 · {1}", n, Fmt.Size(bytes));
            Host.UpdateSelection(n, bytes);
        }

        public List<CleanTarget> SelectedTargets()
        {
            List<CleanTarget> list = new List<CleanTarget>();
            if (_scan != null)
            {
                foreach (CleanTarget t in _scan.Targets)
                    if (t.Exists && t.Selected && t.Deletable) list.Add(t);
            }
            return list;
        }

        private string ShortCat(string id)
        {
            switch (id)
            {
                case "sys": return "系统";
                case "dev": return "开发";
                case "comp": return "组件";
                case "game": return "游戏";
                case "app": return "应用";
                case "user": return "用户";
                default: return id;
            }
        }

        private Color CatColor(string id)
        {
            foreach (CategoryDef c in _cats) if (c.Id == id) return c.Color;
            return Theme.Muted;
        }

        private void GridCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            DataGridViewRow row = _grid.Rows[e.RowIndex];
            CleanTarget t = row.Tag as CleanTarget;
            if (t == null) return;

            if (e.ColumnIndex == 3)
            {
                e.PaintBackground(e.CellBounds, true);
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int cy = e.CellBounds.Y + e.CellBounds.Height / 2;
                using (SolidBrush b = new SolidBrush(CatColor(t.Cat)))
                    g.FillEllipse(b, e.CellBounds.X + Theme.S(4), cy - Theme.S(4), Theme.S(8), Theme.S(8));
                Rectangle tr = new Rectangle(e.CellBounds.X + Theme.S(16), e.CellBounds.Y,
                    e.CellBounds.Width - Theme.S(18), e.CellBounds.Height);
                TextRenderer.DrawText(g, ShortCat(t.Cat), Theme.F(8.5f), tr, Theme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                e.Handled = true;
                return;
            }

            if (e.ColumnIndex == 6)
            {
                e.PaintBackground(e.CellBounds, true);
                Graphics g = e.Graphics;
                string txt = RiskText.Of(t.Risk);
                Font f = Theme.F(8f);
                int w = Theme.BadgeWidth(g, txt, f);
                Rectangle r = new Rectangle(e.CellBounds.X + Theme.S(3),
                    e.CellBounds.Y + (e.CellBounds.Height - Theme.S(20)) / 2, w, Theme.S(20));
                Theme.FillRound(g, r, Theme.S(10), RiskText.Back(t.Risk));
                TextRenderer.DrawText(g, txt, f, r, RiskText.Fore(t.Risk),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                e.Handled = true;
            }
        }
    }
}
