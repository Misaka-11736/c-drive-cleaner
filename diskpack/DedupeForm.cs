using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CCleaner.Disks
{
    public class DedupeForm : Form
    {
        private ListView _groups;
        private ListView _details;
        private Label _status;
        private Label _summary;
        private RoundButton _start;
        private RoundButton _cancel;
        private RoundButton _export;
        private SlimProgress _progress;
        private SplitContainer _split;
        private Panel _body;

        private DedupeResult _result;
        private CancellationTokenSource _cts;
        private bool _busy;
        private string[] _roots;

        public DedupeForm() : this(null)
        {
        }

        public DedupeForm(string[] roots)
        {
            _roots = roots;
            Text = "全盘查重 · C盘清理助手";
            Font = Theme.F(9f);
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Bg;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(Theme.S(1180), Theme.S(780));
            MinimumSize = new Size(Theme.S(980), Theme.S(640));
            KeyPreview = true;

            GradientPanel hero = new GradientPanel();
            hero.Dock = DockStyle.Top;
            hero.Height = Theme.S(74);
            hero.Radius = 0;
            hero.Paint += delegate(object s, PaintEventArgs e)
            {
                TextRenderer.DrawText(e.Graphics, "全盘查重", Theme.F(16f, FontStyle.Bold),
                    new Rectangle(Theme.S(28), Theme.S(12), Theme.S(420), Theme.S(30)), Color.White,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(e.Graphics,
                    "C:\\Users + D/E/F · ≥1 MB · 同盘与跨盘 · 只读报告 · v" + VersionInfo.Version,
                    Theme.F(8.5f), new Rectangle(Theme.S(30), Theme.S(42), Theme.S(720), Theme.S(22)),
                    Color.FromArgb(199, 210, 254), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };

            _body = new Panel();
            _body.Dock = DockStyle.Fill;
            _body.BackColor = Theme.Bg;

            Controls.Add(_body);
            Controls.Add(hero);

            Panel toolbar = new Panel();
            toolbar.BackColor = Theme.Bg;
            _body.Controls.Add(toolbar);

            _start = Button("开始查重", true);
            _start.Width = Theme.S(110);
            _start.Click += delegate { StartScan(); };
            toolbar.Controls.Add(_start);

            _cancel = Button("取消", false);
            _cancel.Width = Theme.S(70);
            _cancel.Visible = false;
            _cancel.Click += delegate { if (_cts != null) _cts.Cancel(); };
            toolbar.Controls.Add(_cancel);

            _export = Button("导出 CSV", false);
            _export.Width = Theme.S(104);
            _export.Enabled = false;
            _export.Click += delegate { Export(); };
            toolbar.Controls.Add(_export);

            _status = new Label();
            _status.AutoSize = false;
            _status.BackColor = Color.Transparent;
            _status.ForeColor = Theme.Muted;
            _status.Font = Theme.F(8.5f);
            _status.TextAlign = ContentAlignment.MiddleRight;
            toolbar.Controls.Add(_status);

            _split = new SplitContainer();
            _split.Orientation = Orientation.Horizontal;
            _split.SplitterWidth = Theme.S(6);
            _split.BackColor = Theme.Bg;
            _body.Controls.Add(_split);

            _groups = new ListView();
            _groups.Dock = DockStyle.Fill;
            _groups.View = View.Details;
            _groups.FullRowSelect = true;
            _groups.MultiSelect = false;
            _groups.BorderStyle = BorderStyle.None;
            _groups.BackColor = Color.White;
            _groups.Font = Theme.F(9f);
            _groups.Columns.Add("组大小", Theme.S(100));
            _groups.Columns.Add("份数", Theme.S(60));
            _groups.Columns.Add("涉及盘", Theme.S(100));
            _groups.Columns.Add("可回收", Theme.S(110));
            _groups.Columns.Add("建议保留", Theme.S(260));
            _groups.Columns.Add("说明", Theme.S(220));
            _groups.SelectedIndexChanged += delegate { ShowDetails(); };
            _split.Panel1.Controls.Add(_groups);

            _details = new ListView();
            _details.Dock = DockStyle.Fill;
            _details.View = View.Details;
            _details.FullRowSelect = true;
            _details.MultiSelect = false;
            _details.BorderStyle = BorderStyle.None;
            _details.BackColor = Color.White;
            _details.Font = Theme.F(8.5f);
            _details.Columns.Add("盘", Theme.S(50));
            _details.Columns.Add("路径", Theme.S(700));
            _details.Columns.Add("大小", Theme.S(90));
            _details.Columns.Add("修改时间", Theme.S(140));
            _details.Columns.Add("链接", Theme.S(60));
            _split.Panel2.Controls.Add(_details);

            _summary = new Label();
            _summary.AutoSize = false;
            _summary.BackColor = Color.Transparent;
            _summary.ForeColor = Theme.Muted;
            _summary.Font = Theme.F(8.5f);
            _summary.TextAlign = ContentAlignment.MiddleLeft;
            _summary.AutoEllipsis = true;
            _body.Controls.Add(_summary);

            _progress = new SlimProgress();
            _progress.Height = Theme.S(6);
            _progress.Value = 0;
            _body.Controls.Add(_progress);

            FormClosing += delegate
            {
                if (_cts != null)
                {
                    try { _cts.Cancel(); } catch { }
                }
            };

            _body.Resize += delegate { LayoutBody(); };
            Resize += delegate { LayoutBody(); };
            LayoutBody();

            _status.Text = "尚未开始。本机约 10–60 分钟，视候选量而定；可随时取消，全程只读。";
        }

        public void AutoStart()
        {
            Shown += delegate { StartScan(); };
        }

        private RoundButton Button(string text, bool primary)
        {
            RoundButton b = new RoundButton();
            b.Text = text;
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

        private void LayoutBody()
        {
            if (_body == null || _groups == null) return;

            int pad = Theme.S(16);
            int w = Math.Max(Theme.S(200), _body.ClientSize.Width - pad * 2);
            int h = _body.ClientSize.Height;

            Panel toolbar = _start.Parent as Panel;
            if (toolbar != null)
            {
                toolbar.Bounds = new Rectangle(pad, Theme.S(8), w, Theme.S(44));
                _start.Location = new Point(0, Theme.S(4));
                _cancel.Location = new Point(_start.Right + Theme.S(8), Theme.S(4));
                _export.Location = new Point((_cancel.Visible ? _cancel.Right : _start.Right) + Theme.S(8), Theme.S(4));
                _status.Bounds = new Rectangle(_export.Right + Theme.S(12), Theme.S(4),
                    Math.Max(Theme.S(100), toolbar.Width - _export.Right - Theme.S(12)), Theme.S(32));
            }

            int top = Theme.S(58);
            int summaryH = Theme.S(34);
            int progressH = Theme.S(6);
            int splitH = Math.Max(Theme.S(240), h - top - summaryH - progressH - Theme.S(12));

            _split.Bounds = new Rectangle(pad, top, w, splitH);
            _summary.Bounds = new Rectangle(pad, top + splitH + Theme.S(2), w, summaryH - Theme.S(4));
            _progress.Bounds = new Rectangle(pad, h - progressH, w, progressH);

            int detailsH = Math.Min(Theme.S(240), Math.Max(Theme.S(120), splitH / 3));
            int dist = splitH - detailsH;
            if (dist < Theme.S(80)) dist = Theme.S(80);
            if (dist > splitH - Theme.S(80)) dist = splitH - Theme.S(80);
            try { _split.SplitterDistance = Math.Max(1, dist); }
            catch { }

            _groups.Columns[0].Width = Theme.S(100);
            _groups.Columns[1].Width = Theme.S(60);
            _groups.Columns[2].Width = Theme.S(100);
            _groups.Columns[3].Width = Theme.S(110);
            _groups.Columns[4].Width = Theme.S(260);
            _groups.Columns[5].Width = Math.Max(Theme.S(200),
                _groups.ClientSize.Width - Theme.S(630));
            _details.Columns[1].Width = Math.Max(Theme.S(260),
                _details.ClientSize.Width - Theme.S(340));
        }

        private void StartScan()
        {
            if (_busy) return;

            _cts = new CancellationTokenSource();
            SetBusy(true);
            _groups.Items.Clear();
            _details.Items.Clear();
            _export.Enabled = false;
            _result = null;
            _progress.Value = 5;
            _summary.Text = "正在准备...";

            CancellationToken token = _cts.Token;
            Stopwatch sw = Stopwatch.StartNew();

            Progress<DedupeProgress> progress = new Progress<DedupeProgress>(delegate(DedupeProgress p)
            {
                if (IsDisposed || !_busy) return;
                _status.Text = string.Format("{0} · {1} 文件 / {2} · 组 {3}",
                    p.Phase, Fmt.Count(p.Files), Fmt.Size(p.Bytes), p.Groups);
                if (p.Current.Length > 0) _status.Text += " · " + ShortPath(p.Current);
                _progress.Value = p.Phase.StartsWith("索引") ? 15 : 55;
            });

            Thread th = new Thread(delegate()
            {
                try
                {
                    DedupeResult r = new AllVolumeDeduper(_roots).Scan(progress, token);
                    RunOnUi(new Action(delegate()
                    {
                        sw.Stop();
                        _result = r;
                        SetBusy(false);
                        Bind(r);
                        _progress.Value = 100;
                        _status.Text = (r.Canceled ? "已取消" : "扫描完成") + " · 用时 " + Fmt.Duration(TimeSpan.FromSeconds(r.Seconds));
                    }));
                }
                catch (Exception ex)
                {
                    RunOnUi(new Action(delegate()
                    {
                        SetBusy(false);
                        MessageBox.Show(this, "查重失败：" + ex.Message, "全盘查重", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
            th.IsBackground = true;
            th.Start();
        }

        private void RunOnUi(Action a)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(a); } catch { }
        }

        private void Bind(DedupeResult r)
        {
            _groups.BeginUpdate();
            _groups.Items.Clear();

            foreach (DedupeGroup g in r.Groups)
            {
                ListViewItem li = new ListViewItem(new string[]
                {
                    Fmt.Size(g.Size),
                    g.Files.Count.ToString(),
                    g.VolumeList,
                    g.HardlinkOnly ? "0 B（硬链接）" : Fmt.Size(g.Reclaimable),
                    g.Keep == null ? "—" : g.Keep.Path,
                    g.HardlinkOnly ? "同一份数据的多个硬链接路径" : g.Reason
                });
                li.Tag = g;
                if (g.HardlinkOnly) li.ForeColor = Theme.Muted;
                _groups.Items.Add(li);
            }
            _groups.EndUpdate();

            _summary.Text = string.Format("扫描 {0} 文件 / {1} · ≥1 MB 候选 {2} · 组 {3} · 可回收 {4} · 硬链接组 {5}",
                Fmt.Count(r.TotalFiles), Fmt.Size(r.TotalBytes), Fmt.Count(r.CandidateFiles),
                r.Groups.Count, Fmt.Size(r.ReclaimableBytes), r.HardlinkGroups);

            if (r.Groups.Count > 0)
            {
                _groups.Items[0].Selected = true;
                _groups.Select();
            }

            _export.Enabled = r.Groups.Count > 0;
        }

        private void ShowDetails()
        {
            _details.BeginUpdate();
            _details.Items.Clear();

            if (_groups.SelectedItems.Count > 0)
            {
                DedupeGroup g = _groups.SelectedItems[0].Tag as DedupeGroup;
                if (g != null)
                {
                    foreach (DedupeFile f in g.Files)
                    {
                        ListViewItem li = new ListViewItem(new string[]
                        {
                            f.Volume + ":",
                            f.Path,
                            Fmt.Size(f.Size),
                            f.Modified.ToString("yyyy-MM-dd HH:mm"),
                            f.Links.ToString()
                        });
                        if (f.Links > 1) li.ForeColor = Theme.Muted;
                        _details.Items.Add(li);
                    }
                }
            }
            _details.EndUpdate();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _start.Enabled = !busy;
            _cancel.Visible = busy;
            _groups.Enabled = !busy;
            _details.Enabled = !busy;
            LayoutBody();
        }

        private void Export()
        {
            if (_result == null || _result.Groups.Count == 0) return;

            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "CSV 文件 (*.csv)|*.csv";
                dlg.FileName = "dedupe-groups.csv";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string dir = Path.GetDirectoryName(dlg.FileName);
                string groupsPath = dlg.FileName;
                string filesPath = Path.Combine(dir, "dedupe-files.csv");

                try
                {
                    StringBuilder gs = new StringBuilder();
                    gs.AppendLine("group,size_bytes,copies,distinct_ids,volumes,reclaimable_bytes,hardlink_only,suggested_keep,reason");
                    StringBuilder fs = new StringBuilder();
                    fs.AppendLine("group,volume,path,size_bytes,modified,links,hardlink");

                    int index = 0;
                    foreach (DedupeGroup g in _result.Groups)
                    {
                        index++;
                        gs.AppendLine(string.Join(",", new string[]
                        {
                            index.ToString(),
                            g.Size.ToString(),
                            g.Files.Count.ToString(),
                            g.DistinctIds.ToString(),
                            Csv(g.VolumeList),
                            g.Reclaimable.ToString(),
                            g.HardlinkOnly ? "true" : "false",
                            Csv(g.Keep == null ? "" : g.Keep.Path),
                            Csv(g.Reason)
                        }));

                        foreach (DedupeFile f in g.Files)
                        {
                            fs.AppendLine(string.Join(",", new string[]
                            {
                                index.ToString(),
                                f.Volume,
                                Csv(f.Path),
                                f.Size.ToString(),
                                f.Modified.ToString("yyyy-MM-dd HH:mm:ss"),
                                f.Links.ToString(),
                                f.Links > 1 ? "true" : "false"
                            }));
                        }
                    }

                    File.WriteAllText(groupsPath, gs.ToString(), new UTF8Encoding(true));
                    File.WriteAllText(filesPath, fs.ToString(), new UTF8Encoding(true));

                    MessageBox.Show(this, "已导出：\r\n" + groupsPath + "\r\n" + filesPath,
                        "全盘查重", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "导出失败：" + ex.Message, "全盘查重",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string Csv(string s)
        {
            if (s == null) return "";
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        private static string ShortPath(string path)
        {
            if (path == null) return "";
            if (path.Length <= 70) return path;
            return "..." + path.Substring(path.Length - 67);
        }
    }
}
