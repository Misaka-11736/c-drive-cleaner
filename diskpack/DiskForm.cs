using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CCleaner.Disks
{
    public class DiskForm : Form
    {
        private readonly List<VolumeInfo> _volumes = new List<VolumeInfo>();
        private readonly Dictionary<string, VolumeScanResult> _results = new Dictionary<string, VolumeScanResult>(StringComparer.OrdinalIgnoreCase);
        private VolumeInfo _current;

        private DataGridView _volGrid;
        private ListView _grid;
        private Panel _right;
        private Panel _gridCard;
        private Label _title;
        private Label _subtitle;
        private Label _warning;
        private Label _summary;
        private Label _status;
        private Panel _toolbar;
        private RoundButton _quick;
        private RoundButton _deep;
        private RoundButton _stop;
        private RoundButton _clean;
        private SlimProgress _progress;

        private CancellationTokenSource _cts;
        private bool _busy;

        public DiskForm()
        {
            Text = "磁盘扩展包 · C盘清理助手";
            Font = Theme.F(9f);
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Bg;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(Theme.S(1180), Theme.S(760));
            MinimumSize = new Size(Theme.S(980), Theme.S(640));
            KeyPreview = true;

            BuildUi();
            LoadVolumes();
        }

        private void BuildUi()
        {
            GradientPanel hero = new GradientPanel();
            hero.Dock = DockStyle.Top;
            hero.Height = Theme.S(74);
            hero.Radius = 0;
            hero.Paint += delegate(object s, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                TextRenderer.DrawText(g, "磁盘扩展包", Theme.F(16f, FontStyle.Bold),
                    new Rectangle(Theme.S(28), Theme.S(12), Theme.S(420), Theme.S(30)), Color.White,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, "可选模块 · 多盘分析 · 只删除安全项 · v" + VersionInfo.Version,
                    Theme.F(8.5f), new Rectangle(Theme.S(30), Theme.S(42), Theme.S(620), Theme.S(22)),
                    Color.FromArgb(199, 210, 254), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };
            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.Padding = new Padding(Theme.S(16), Theme.S(12), Theme.S(16), Theme.S(10));
            body.BackColor = Theme.Bg;
            Controls.Add(body);
            Controls.Add(hero);

            Panel left = new Panel();
            left.Dock = DockStyle.Left;
            left.Width = Theme.S(300);
            left.Padding = new Padding(0, 0, Theme.S(12), 0);

            CardPanel volCard = new CardPanel();
            volCard.Dock = DockStyle.Fill;
            volCard.Padding = new Padding(Theme.S(8));
            left.Controls.Add(volCard);

            _volGrid = new DataGridView();
            _volGrid.Dock = DockStyle.Fill;
            Theme.StyleGrid(_volGrid);
            _volGrid.RowTemplate.Height = Theme.S(44);
            _volGrid.ReadOnly = true;
            _volGrid.MultiSelect = false;
            _volGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            AddVolCol("盘符", Theme.S(52));
            AddVolCol("类型", Theme.S(104));
            AddVolCol("可用", Theme.S(112));
            _volGrid.Columns[1].DefaultCellStyle.Font = Theme.F(8f);
            _volGrid.Columns[2].DefaultCellStyle.Font = Theme.F(8f);
            _volGrid.SelectionChanged += delegate { SelectVolume(); };
            volCard.Controls.Add(_volGrid);

            Panel right = new Panel();
            right.Dock = DockStyle.Fill;
            body.Controls.Add(right);
            _right = right;
            right.Resize += delegate { LayoutToolbar(); };
            body.Controls.Add(left);

            Panel toolbar = new Panel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = Theme.S(52);
            toolbar.BackColor = Theme.Bg;
            right.Controls.Add(toolbar);
            _toolbar = toolbar;
            toolbar.Resize += delegate { LayoutToolbar(); };

            _title = new Label();
            _title.AutoSize = false;
            _title.BackColor = Color.Transparent;
            _title.ForeColor = Theme.Text;
            _title.Font = Theme.F(12f, FontStyle.Bold);
            _title.TextAlign = ContentAlignment.MiddleLeft;
            _title.Location = new Point(0, 0);
            _title.Size = new Size(Theme.S(520), Theme.S(28));
            toolbar.Controls.Add(_title);

            _subtitle = new Label();
            _subtitle.AutoSize = false;
            _subtitle.BackColor = Color.Transparent;
            _subtitle.ForeColor = Theme.Muted;
            _subtitle.Font = Theme.F(8.5f);
            _subtitle.TextAlign = ContentAlignment.MiddleLeft;
            _subtitle.Location = new Point(Theme.S(1), Theme.S(27));
            _subtitle.Size = new Size(Theme.S(520), Theme.S(22));
            toolbar.Controls.Add(_subtitle);

            _clean = SmallButton("清理选中项", true);
            _clean.Enabled = false;
            _clean.Width = Theme.S(110);
            _clean.Click += delegate { CleanSelected(); };
            toolbar.Controls.Add(_clean);

            _stop = SmallButton("停止", false);
            _stop.Width = Theme.S(64);
            _stop.Visible = false;
            _stop.Click += delegate { if (_cts != null) _cts.Cancel(); };
            toolbar.Controls.Add(_stop);

            _deep = SmallButton("深度扫描", false);
            _deep.Width = Theme.S(92);
            _deep.Click += delegate { StartScan(true); };
            toolbar.Controls.Add(_deep);

            _quick = SmallButton("快速扫描", false);
            _quick.Width = Theme.S(92);
            _quick.Click += delegate { StartScan(false); };
            toolbar.Controls.Add(_quick);

            _warning = new Label();
            _warning.AutoSize = false;
            _warning.BackColor = Color.FromArgb(255, 247, 237);
            _warning.ForeColor = Color.FromArgb(154, 52, 18);
            _warning.Font = Theme.F(8.5f);
            _warning.TextAlign = ContentAlignment.MiddleLeft;
            _warning.Dock = DockStyle.Top;
            _warning.Height = Theme.S(0);
            _warning.Visible = false;
            right.Controls.Add(_warning);

            _gridCard = new Panel();
            Panel gridCard = _gridCard;
            gridCard.BackColor = Color.White;
            gridCard.Padding = new Padding(Theme.S(10), Theme.S(8), Theme.S(10), Theme.S(10));
            right.Controls.Add(gridCard);

            _grid = new ListView();
            _grid.Dock = DockStyle.None;
            _grid.View = View.Details;
            _grid.CheckBoxes = true;
            _grid.FullRowSelect = true;
            _grid.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _grid.BorderStyle = BorderStyle.None;
            _grid.BackColor = Color.White;
            _grid.Font = Theme.F(9f);
            _grid.Columns.Add("", Theme.S(34));
            _grid.Columns.Add("分类", Theme.S(84));
            _grid.Columns.Add("项目", Theme.S(150));
            _grid.Columns.Add("路径", Theme.S(420));
            _grid.Columns.Add("大小", Theme.S(88));
            _grid.Columns.Add("文件数", Theme.S(64));
            _grid.Columns.Add("说明", Theme.S(230));
            _grid.ItemCheck += delegate(object s, ItemCheckEventArgs e)
            {
                VolumeItem it = _grid.Items[e.Index].Tag as VolumeItem;
                if (it != null && !it.Deletable) e.NewValue = CheckState.Unchecked;
            };
            _grid.ItemChecked += delegate { UpdateSummary(); };
            gridCard.Controls.Add(_grid);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = Theme.S(46);
            bottom.BackColor = Theme.Bg;
            right.Controls.Add(bottom);

            _summary = new Label();
            _summary.AutoSize = false;
            _summary.BackColor = Color.Transparent;
            _summary.ForeColor = Theme.Muted;
            _summary.Font = Theme.F(8.5f);
            _summary.TextAlign = ContentAlignment.MiddleLeft;
            _summary.AutoEllipsis = true;
            _summary.Dock = DockStyle.Fill;
            bottom.Controls.Add(_summary);

            _status = new Label();
            _status.AutoSize = false;
            _status.BackColor = Color.Transparent;
            _status.ForeColor = Theme.Muted;
            _status.Font = Theme.F(8.5f);
            _status.TextAlign = ContentAlignment.MiddleRight;
            _status.Dock = DockStyle.Right;
            _status.Width = Theme.S(420);
            bottom.Controls.Add(_status);

            _progress = new SlimProgress();
            _progress.Height = Theme.S(6);
            _progress.Dock = DockStyle.Bottom;
            _progress.Value = 0;
            right.Controls.Add(_progress);
            toolbar.BringToFront();

            Resize += delegate { LayoutToolbar(); };
            LayoutToolbar();
        }

        private void AddVolCol(string header, int width)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = header;
            c.Width = width;
            c.ReadOnly = true;
            c.SortMode = DataGridViewColumnSortMode.NotSortable;
            _volGrid.Columns.Add(c);
        }

        private RoundButton SmallButton(string text, bool primary)
        {
            RoundButton b = new RoundButton();
            b.Text = text;
            b.Width = Theme.S(96);
            b.Height = Theme.S(32);
            b.Font = Theme.F(8.5f);
            if (primary)
            {
                b.Fill = Theme.Primary;
                b.HoverFill = Theme.PrimaryDark;
                b.TextColor = Color.White;
                b.BorderColor = Color.Transparent;
            }
            return b;
        }

        private void LayoutToolbar()
        {
            if (_quick == null) return;
            int right = (_toolbar != null ? _toolbar.Width : Width) - Theme.S(14);
            int y = Theme.S(10);

            if (_gridCard != null && _right != null && _grid != null)
            {
                int top = Theme.S(52) + (_warning != null && _warning.Visible ? Theme.S(30) : 0);
                int bottom = Theme.S(46) + Theme.S(6);
                _gridCard.Bounds = new Rectangle(0, top, _right.Width,
                    Math.Max(Theme.S(160), _right.Height - top - bottom));
                _grid.Bounds = new Rectangle(Theme.S(8), Theme.S(38),
                    Math.Max(Theme.S(200), _gridCard.Width - Theme.S(16)),
                    Math.Max(Theme.S(100), _gridCard.Height - Theme.S(46)));
                if (_grid.Columns.Count == 7 && _grid.ClientSize.Width > Theme.S(300))
                {
                    int fixedW = Theme.S(34) + Theme.S(84) + Theme.S(150) + Theme.S(64) + Theme.S(230) + Theme.S(88);
                    _grid.Columns[0].Width = Theme.S(34);
                    _grid.Columns[1].Width = Theme.S(84);
                    _grid.Columns[2].Width = Theme.S(150);
                    _grid.Columns[4].Width = Theme.S(88);
                    _grid.Columns[5].Width = Theme.S(64);
                    _grid.Columns[6].Width = Theme.S(230);
                    _grid.Columns[3].Width = Math.Max(Theme.S(200), _grid.ClientSize.Width - fixedW - Theme.S(4));
                }
            }
            _clean.Location = new Point(right - _clean.Width, y);
            right -= _clean.Width + Theme.S(8);
            _deep.Location = new Point(right - _deep.Width, y);
            right -= _deep.Width + Theme.S(8);
            _quick.Location = new Point(right - _quick.Width, y);
            right -= _quick.Width + Theme.S(8);
            _stop.Location = new Point(right - _stop.Width, y);
        }

        private void LoadVolumes()
        {
            _volumes.Clear();
            _volumes.AddRange(VolumeUtil.GetVolumes());

            _volGrid.Rows.Clear();
            foreach (VolumeInfo v in _volumes)
            {
                int i = _volGrid.Rows.Add();
                DataGridViewRow row = _volGrid.Rows[i];
                row.Cells[0].Value = v.Letter + ":";
                row.Cells[1].Value = v.TypeText;
                row.Cells[2].Value = Fmt.Size(v.Free);
                row.Tag = v;
            }

            if (_volumes.Count == 0)
            {
                _title.Text = "没有可扫描的磁盘";
                _subtitle.Text = "只列出固定磁盘与可移动磁盘，网络盘和光驱不参与。";
                return;
            }

            _volGrid.Rows[0].Selected = true;
            _volGrid.CurrentCell = _volGrid.Rows[0].Cells[0];
            SelectVolume();
        }

        private void SelectVolume()
        {
            if (_volGrid.SelectedRows.Count == 0) return;
            VolumeInfo v = _volGrid.SelectedRows[0].Tag as VolumeInfo;
            if (v == null) return;
            _current = v;

            _title.Text = v.Letter + ":  " + (string.IsNullOrEmpty(v.Label) ? "本地磁盘" : v.Label);
            _subtitle.Text = v.TypeText + " · " + v.FileSystem + " · " + Fmt.Size(v.Free) + " 可用 / " + Fmt.Size(v.Total) +
                (string.IsNullOrEmpty(v.Serial) ? "" : " · SN " + v.Serial);

            _warning.Visible = v.IsBackup || v.IsExternal;
            if (v.IsBackup)
            {
                _warning.Text = "  这是备份盘（检测到备份目录或卷标），只允许清空回收站，其余项目一律只读。";
                _warning.Height = Theme.S(30);
            }
            else if (v.IsExternal)
            {
                _warning.Text = "  外接设备：扫描和清理期间请不要拔线，否则可能损坏文件系统。";
                _warning.Height = Theme.S(30);
            }
            else
            {
                _warning.Height = 0;
            }

            VolumeScanResult r;
            if (_results.TryGetValue(v.Letter, out r)) Bind(r);
            else
            {
                _grid.Items.Clear();
                _summary.Text = "尚未扫描。快速扫描只统计体积；深度扫描会额外找大文件、重复文件和长期未用文件。";
            }

            UpdateButtons();
        }

        public void SelectAndScan(string letter, bool deep)
        {
            foreach (DataGridViewRow row in _volGrid.Rows)
            {
                VolumeInfo v = row.Tag as VolumeInfo;
                if (v == null) continue;
                if (!string.Equals(v.Letter, letter, StringComparison.OrdinalIgnoreCase)) continue;
                row.Selected = true;
                _volGrid.CurrentCell = row.Cells[0];
                SelectVolume();
                StartScan(deep);
                return;
            }
        }

        private void RunOnUi(Action a)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(a); } catch { }
        }

        private void StartScan(bool deep)
        {
            if (_busy || _current == null) return;
            _cts = new CancellationTokenSource();
            SetBusy(true);
            _progress.Value = -1;
            _status.Text = deep ? "深度扫描中（会读取文件做查重，较慢）..." : "快速扫描中...";

            VolumeInfo vol = _current;
            bool isDeep = deep;
            CancellationToken token = _cts.Token;

            Progress<ScanProgress> progress = new Progress<ScanProgress>(delegate(ScanProgress p)
            {
                if (!_busy) return;
                _status.Text = string.Format("扫描中 {0} 文件 / {1} 目录 / {2}",
                    Fmt.Count(p.Files), Fmt.Count(p.Dirs), Fmt.Size(p.Bytes));
            });

            Stopwatch sw = Stopwatch.StartNew();
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 500;
            timer.Tick += delegate { if (_busy) _status.Text = "扫描中 · 已用 " + Fmt.Duration(sw.Elapsed) + " · " + _current.Letter + ":"; };
            timer.Start();

            Thread th = new Thread(delegate()
            {
                try
                {
                    VolumeScanResult r = new VolumeScanner().Scan(vol, isDeep, progress, token);
                    RunOnUi(new Action(delegate()
                    {
                        timer.Stop();
                        timer.Dispose();
                        SetBusy(false);
                        _results[vol.Letter] = r;
                        Bind(r);
                        _status.Text = "扫描完成，用时 " + Fmt.Duration(TimeSpan.FromSeconds(r.Seconds));
                    }));
                }
                catch (Exception ex)
                {
                    RunOnUi(new Action(delegate()
                    {
                        timer.Stop();
                        timer.Dispose();
                        SetBusy(false);
                        MessageBox.Show(this, "扫描失败：" + ex.Message, "磁盘扩展包", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
            th.IsBackground = true;
            th.Start();
        }

        private void Bind(VolumeScanResult r)
        {
            _grid.BeginUpdate();
            _grid.Items.Clear();
            foreach (VolumeItem item in r.Items)
            {
                ListViewItem li = new ListViewItem(new string[]
                {
                    "", item.Category, item.Label, item.Path,
                    Fmt.Size(item.Bytes),
                    item.Files > 0 ? Fmt.Count(item.Files) : "",
                    item.Note
                });
                li.Tag = item;
                li.Checked = item.Deletable;
                if (!item.Deletable) li.ForeColor = Theme.Muted;
                _grid.Items.Add(li);
            }
            _grid.EndUpdate();
            UpdateSummary();
        }

        private List<VolumeItem> SelectedItems()
        {
            List<VolumeItem> list = new List<VolumeItem>();
            foreach (ListViewItem li in _grid.CheckedItems)
            {
                VolumeItem item = li.Tag as VolumeItem;
                if (item == null || !item.Deletable) continue;
                list.Add(item);
            }
            return list;
        }

        private void UpdateSummary()
        {
            VolumeScanResult r;
            if (_current == null || !_results.TryGetValue(_current.Letter, out r))
            {
                return;
            }

            long sel = 0;
            int n = 0;
            foreach (VolumeItem item in SelectedItems()) { sel += item.Bytes; n++; }

            _summary.Text = string.Format("总占用 {0} · 可清理 {1} · 仅报告 {2} · 已选 {3} 项 {4}",
                Fmt.Size(r.TotalBytes), Fmt.Size(r.DeletableBytes()), Fmt.Size(r.ReportBytes()), n, Fmt.Size(sel));

            _clean.Enabled = !_busy && n > 0 && _current != null;
        }

        private void CleanSelected()
        {
            if (_busy || _current == null) return;
            List<VolumeItem> items = SelectedItems();
            if (items.Count == 0) return;

            long bytes = 0;
            foreach (VolumeItem i in items) bytes += i.Bytes;

            bool hasSteam = false;
            foreach (VolumeItem i in items)
                if (i.Id.StartsWith("STEAM-", StringComparison.Ordinal)) hasSteam = true;
            if (hasSteam && Process.GetProcessesByName("steam").Length > 0)
            {
                DialogResult sr = MessageBox.Show(this,
                    "Steam 正在运行。清理未完成下载或着色器缓存前，建议先退出 Steam，\r\n" +
                    "否则文件可能被占用，或清理后又被重新生成。\r\n\r\n仍要继续吗？",
                    "Steam 正在运行", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (sr != DialogResult.Yes) return;
            }

            using (VolumeConfirmDialog dlg = new VolumeConfirmDialog(_current, items, bytes))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
            }

            _cts = new CancellationTokenSource();
            SetBusy(true);
            _progress.Value = 0;

            VolumeInfo vol = _current;
            CancellationToken token = _cts.Token;
            Progress<CleanProgress> progress = new Progress<CleanProgress>(delegate(CleanProgress p)
            {
                if (!_busy) return;
                if (p.Total > 0) _progress.Value = (int)(p.Index * 100.0 / p.Total);
                _status.Text = "清理中 " + p.Index + "/" + p.Total + " · " + p.Message;
            });

            Thread th = new Thread(delegate()
            {
                try
                {
                    VolumeCleanResult res = VolumeCleaner.Clean(vol, items, progress, token,
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "C盘清理助手"));
                    RunOnUi(new Action(delegate()
                    {
                        SetBusy(false);
                        vol.Free = new DriveInfo(vol.Root).AvailableFreeSpace;
                        foreach (DataGridViewRow row in _volGrid.Rows)
                        {
                            VolumeInfo rv = row.Tag as VolumeInfo;
                            if (rv != null && rv.Letter == vol.Letter) row.Cells[2].Value = Fmt.Size(rv.Free);
                        }
                        _results.Remove(vol.Letter);
                        _grid.Items.Clear();
                        UpdateSummary();
                        SelectVolume();

                        MessageBox.Show(this, string.Format(
                            "清理完成。\r\n\r\n{0}: 可用 {1} -> {2}\r\n释放 {3}\r\n未删除 {4} 项\r\n\r\n日志：{5}",
                            vol.Letter, Fmt.Size(res.BeforeFree), Fmt.Size(res.AfterFree), Fmt.Size(res.Freed),
                            res.Errors, res.LogPath), "磁盘扩展包", MessageBoxButtons.OK,
                            res.Errors > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                    }));
                }
                catch (Exception ex)
                {
                    RunOnUi(new Action(delegate()
                    {
                        SetBusy(false);
                        MessageBox.Show(this, "清理失败：" + ex.Message, "磁盘扩展包", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
            th.IsBackground = true;
            th.Start();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _quick.Enabled = !busy && _current != null;
            _deep.Enabled = !busy && _current != null;
            _stop.Visible = busy;
            _clean.Enabled = !busy && SelectedItems().Count > 0 && _current != null;
            _volGrid.Enabled = !busy;
            _grid.Enabled = !busy;
            if (!busy) { _progress.Value = 0; UpdateButtons(); }
        }

        private void UpdateButtons()
        {
            _quick.Enabled = !_busy && _current != null;
            _deep.Enabled = !_busy && _current != null;
        }
    }

    public class VolumeConfirmDialog : Form
    {
        private TextBox _input;
        private RoundButton _ok;

        public VolumeConfirmDialog(VolumeInfo v, List<VolumeItem> items, long bytes)
        {
            Text = "确认清理 " + v.Letter + ":";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.White;
            ClientSize = new Size(Theme.S(620), Theme.S(420));
            Font = Theme.F(9f);

            Label title = new Label();
            title.Text = "清理 " + v.Letter + ":  " + (string.IsNullOrEmpty(v.Label) ? "本地磁盘" : v.Label);
            title.Font = Theme.F(14f, FontStyle.Bold);
            title.ForeColor = Theme.Text;
            title.AutoSize = false;
            title.Location = new Point(Theme.S(26), Theme.S(20));
            title.Size = new Size(Theme.S(560), Theme.S(30));
            Controls.Add(title);

            Label sub = new Label();
            sub.Text = string.Format("{0} 项 · {1} · 只删除安全项，其余不会被碰", items.Count, Fmt.Size(bytes));
            sub.ForeColor = Theme.Muted;
            sub.Font = Theme.F(9f);
            sub.AutoSize = false;
            sub.Location = new Point(Theme.S(27), Theme.S(54));
            sub.Size = new Size(Theme.S(560), Theme.S(22));
            Controls.Add(sub);

            Label list = new Label();
            list.Font = Theme.F(8.5f);
            list.ForeColor = Theme.Muted;
            list.AutoSize = false;
            list.Location = new Point(Theme.S(27), Theme.S(88));
            list.Size = new Size(Theme.S(566), Theme.S(170));
            string text = "";
            int shown = 0;
            foreach (VolumeItem i in items)
            {
                if (shown >= 10) { text += "… 还有 " + (items.Count - shown) + " 项\r\n"; break; }
                text += "• " + i.Category + " · " + i.Label + "  " + Fmt.Size(i.Bytes) + "\r\n";
                shown++;
            }
            list.Text = text;
            Controls.Add(list);

            Label warn = new Label();
            warn.Text = "永久删除，不进回收站。请输入 DELETE " + v.Letter + " 确认：";
            warn.Font = Theme.F(9f, FontStyle.Bold);
            warn.ForeColor = Theme.Red;
            warn.AutoSize = false;
            warn.Location = new Point(Theme.S(26), Theme.S(266));
            warn.Size = new Size(Theme.S(560), Theme.S(22));
            Controls.Add(warn);

            _input = new TextBox();
            _input.Font = new Font("Consolas", 12f, FontStyle.Bold, GraphicsUnit.Point);
            _input.Location = new Point(Theme.S(26), Theme.S(292));
            _input.Size = new Size(Theme.S(240), Theme.S(30));
            _input.TextChanged += delegate { RefreshOk(v.Letter); };
            Controls.Add(_input);

            RoundButton cancel = new RoundButton();
            cancel.Text = "取消";
            cancel.Width = Theme.S(84);
            cancel.Height = Theme.S(34);
            cancel.Location = new Point(ClientSize.Width - Theme.S(26) - Theme.S(84), ClientSize.Height - Theme.S(54));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            _ok = new RoundButton();
            _ok.Text = "确认清理";
            _ok.Width = Theme.S(110);
            _ok.Height = Theme.S(34);
            _ok.Fill = Theme.Red;
            _ok.HoverFill = Color.FromArgb(140, 25, 17);
            _ok.TextColor = Color.White;
            _ok.BorderColor = Color.Transparent;
            _ok.Enabled = false;
            _ok.Location = new Point(cancel.Left - Theme.S(10) - _ok.Width, cancel.Top);
            _ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(_ok);

            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
                else if (e.KeyCode == Keys.Enter && _ok.Enabled) { DialogResult = DialogResult.OK; Close(); }
            };
        }

        private void RefreshOk(string letter)
        {
            _ok.Enabled = string.Equals(_input.Text.Trim(), "DELETE " + letter, StringComparison.OrdinalIgnoreCase);
        }
    }
}
