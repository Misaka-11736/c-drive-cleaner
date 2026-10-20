using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CCleaner
{
    public class ConfirmDialog : Form
    {
        public bool DryRun { get; private set; }

        private TextBox _input;
        private RoundButton _ok;
        private CheckBox _dry;

        public ConfirmDialog(List<CleanTarget> items, long total)
        {
            Text = "确认清理";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            AutoScaleMode = AutoScaleMode.None;
            Font = Theme.F(9f);
            ClientSize = new Size(Theme.S(760), Theme.S(560));

            Label title = new Label();
            title.Text = "确认永久删除";
            title.Font = Theme.F(15f, FontStyle.Bold);
            title.ForeColor = Theme.Text;
            title.AutoSize = false;
            title.Location = new Point(Theme.S(24), Theme.S(18));
            title.Size = new Size(Theme.S(400), Theme.S(30));
            Controls.Add(title);

            Label sub = new Label();
            sub.Text = string.Format("共 {0} 项 · 计划释放 {1} · 删除后不可恢复，不进回收站", items.Count, Fmt.Size(total));
            sub.Font = Theme.F(9f);
            sub.ForeColor = Theme.Muted;
            sub.AutoSize = false;
            sub.Location = new Point(Theme.S(25), Theme.S(52));
            sub.Size = new Size(Theme.S(700), Theme.S(22));
            Controls.Add(sub);

            DataGridView grid = new DataGridView();
            grid.Location = new Point(Theme.S(24), Theme.S(84));
            grid.Size = new Size(ClientSize.Width - Theme.S(48), Theme.S(300));
            Theme.StyleGrid(grid);
            grid.RowTemplate.Height = Theme.S(30);
            grid.ReadOnly = true;
            grid.AllowUserToResizeRows = false;

            DataGridViewTextBoxColumn c1 = new DataGridViewTextBoxColumn();
            c1.HeaderText = "编号";
            c1.Width = Theme.S(66);
            c1.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(c1);

            DataGridViewTextBoxColumn c2 = new DataGridViewTextBoxColumn();
            c2.HeaderText = "项目";
            c2.Width = Theme.S(130);
            c2.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(c2);

            DataGridViewTextBoxColumn c3 = new DataGridViewTextBoxColumn();
            c3.HeaderText = "大小";
            c3.Width = Theme.S(88);
            c3.SortMode = DataGridViewColumnSortMode.NotSortable;
            c3.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns.Add(c3);

            DataGridViewTextBoxColumn c4 = new DataGridViewTextBoxColumn();
            c4.HeaderText = "路径";
            c4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            c4.SortMode = DataGridViewColumnSortMode.NotSortable;
            c4.DefaultCellStyle.ForeColor = Theme.Muted;
            c4.DefaultCellStyle.Font = Theme.F(8.5f);
            grid.Columns.Add(c4);

            foreach (CleanTarget t in items)
            {
                int i = grid.Rows.Add();
                DataGridViewRow row = grid.Rows[i];
                row.Cells[0].Value = t.Id;
                row.Cells[1].Value = t.Label;
                row.Cells[2].Value = (t.Mode == TargetMode.Dism || t.Mode == TargetMode.Shadow)
                    ? "按实际"
                    : (t.Denied ? "需管理员" : Fmt.Size(t.Bytes));
                row.Cells[3].Value = t.Paths.Count > 0 ? t.Paths[0] : "（系统级操作）";
            }
            Controls.Add(grid);

            Label warn = new Label();
            warn.Text = "删除后无法恢复。请输入 DELETE 以确认，或勾选下方「仅演练」。";
            warn.Font = Theme.F(9f, FontStyle.Bold);
            warn.ForeColor = Theme.Red;
            warn.AutoSize = false;
            warn.Location = new Point(Theme.S(24), Theme.S(398));
            warn.Size = new Size(Theme.S(700), Theme.S(22));
            Controls.Add(warn);

            _input = new TextBox();
            _input.Location = new Point(Theme.S(24), Theme.S(426));
            _input.Size = new Size(Theme.S(220), Theme.S(30));
            _input.Font = new Font("Consolas", 12f, FontStyle.Bold, GraphicsUnit.Point);
            _input.BorderStyle = BorderStyle.FixedSingle;
            _input.TextChanged += delegate { RefreshOk(); };
            Controls.Add(_input);

            _dry = new CheckBox();
            _dry.Text = "仅演练：不实际删除，只统计";
            _dry.Font = Theme.F(9f);
            _dry.ForeColor = Theme.Muted;
            _dry.AutoSize = true;
            _dry.Location = new Point(Theme.S(262), Theme.S(430));
            _dry.CheckedChanged += delegate { RefreshOk(); };
            Controls.Add(_dry);

            RoundButton cancel = new RoundButton();
            cancel.Text = "取消";
            cancel.Width = Theme.S(84);
            cancel.Height = Theme.S(34);
            cancel.Location = new Point(ClientSize.Width - Theme.S(24) - Theme.S(84), ClientSize.Height - Theme.S(56));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            _ok = new RoundButton();
            _ok.Text = "确认删除";
            _ok.Width = Theme.S(120);
            _ok.Height = Theme.S(34);
            _ok.Fill = Theme.Primary;
            _ok.HoverFill = Theme.PrimaryDark;
            _ok.TextColor = Color.White;
            _ok.BorderColor = Color.Transparent;
            _ok.Enabled = false;
            _ok.Location = new Point(cancel.Left - Theme.S(12) - _ok.Width, cancel.Top);
            _ok.Click += delegate
            {
                DryRun = _dry.Checked;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(_ok);

            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
                else if (e.KeyCode == Keys.Enter && _ok.Enabled)
                {
                    DryRun = _dry.Checked;
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };
            RefreshOk();
        }

        private void RefreshOk()
        {
            if (_ok == null || _input == null || _dry == null) return;
            bool typed = string.Equals(_input.Text.Trim(), "DELETE", StringComparison.OrdinalIgnoreCase);
            _ok.Enabled = typed || _dry.Checked;
            _ok.Text = _dry.Checked ? "开始演练" : "确认删除";
        }
    }
}
