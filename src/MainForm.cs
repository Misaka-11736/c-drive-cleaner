using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CCleaner
{
    public class MainForm : Form
    {
        public const string AppName = "C盘清理助手";
        public const string AppVersion = VersionInfo.Version;

        public readonly string UserProfile;
        public readonly string LocalAppData;
        public readonly string RoamingAppData;
        public readonly string OutDir;

        private Panel _sidebar;
        private Panel _header;
        private Panel _content;
        private Panel _status;
        private Label _pageTitle;
        private Label _pageSub;
        private Label _statusText;
        private Label _selText;
        private RoundButton _btnScan;
        private RoundButton _btnExport;
        private RoundButton _btnImport;
        private RoundButton _btnClean;
        private RoundButton _btnCancel;
        private SlimProgress _progress;
        private List<NavItem> _nav;
        private PageBase[] _pages;
        private OverviewPage _overview;
        private CleanPage _clean;
        private StatsPage _stats;
        private RankPage _rank;
        private SuggestionPage _suggest;
        private LogPage _log;
        private UpdateBanner _banner;
        private UpdateInfo _updateInfo;
        private Label _settingsLink;
        private Label _diskLink;

        private ScanResult _scan;
        private CancellationTokenSource _cts;
        private bool _busy;
        private readonly Stopwatch _workWatch = new Stopwatch();
        private int _pageIndex;
        private int _startPage;

        private readonly string[] _titles = { "总览", "可清理项目", "仅统计", "占用排行", "清理建议", "清理日志" };
        private readonly string[] _subs = {
            "磁盘占用、可清理空间与分类汇总",
            "勾选要删除的项目，默认只勾选推荐项",
            "个人数据与大型软件，只统计不建议删除",
            "目录占用排行与最大的文件",
            "针对这台机器的具体建议",
            "最近一次清理的详细记录"
        };

        public MainForm(string userProfile, string local, string roaming, bool autoScan, int startPage, string openPlan, bool autoDryRun, bool noUpdateCheck)
        {
            UserProfile = userProfile;
            LocalAppData = local;
            RoamingAppData = roaming;
            OutDir = Path.Combine(userProfile, "Documents", "C盘清理助手");
            _startPage = startPage;

            Text = AppName + "  v" + AppVersion;
            Font = Theme.F(9f);
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Bg;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(Theme.S(1280), Theme.S(900));
            MinimumSize = new Size(Theme.S(1000), Theme.S(660));
            KeyPreview = true;
            DoubleBuffered = true;

            BuildUi();
            ShowPage(0);
            UpdateSelection(0, 0);
            SetStatus("就绪 · 点击右上角「重新扫描」开始分析 C 盘");
            SetBusy(false);

            if (autoScan)
            {
                Shown += delegate { BeginScan(); };
            }
            else if (!string.IsNullOrEmpty(openPlan))
            {
                Shown += delegate
                {
                    LoadPlan(openPlan);
                    if (autoDryRun) DryRunPlan();
                };
            }

            if (!noUpdateCheck)
            {
                Shown += delegate { StartUpdateCheck(); };
            }
        }

        // ------------------------------------------------------------------ 更新检查
        private void StartUpdateCheck()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                UpdateInfo info = UpdateChecker.Check(LocalAppData, false);
                if (info == null) return;
                try
                {
                    BeginInvoke(new Action(delegate { ShowUpdateBanner(info); }));
                }
                catch { }
            });
        }

        private void ShowUpdateBanner(UpdateInfo info)
        {
            _updateInfo = info;
            _banner.ShowUpdate(info.Version, VersionInfo.Version);
        }

        // ------------------------------------------------------------------ 磁盘扩展包
        private string FindDiskPack()
        {
            string[] candidates = new string[]
            {
                Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "磁盘扩展包.exe"),
                Path.Combine(LocalAppData, "C盘清理助手", "dlc", "磁盘扩展包.exe")
            };
            foreach (string p in candidates)
            {
                try { if (File.Exists(p)) return p; }
                catch { }
            }
            return null;
        }

        private void OpenDiskPack()
        {
            string module = FindDiskPack();
            if (module != null)
            {
                string moduleVersion = "";
                try { moduleVersion = FileVersionInfo.GetVersionInfo(module).FileVersion; }
                catch { }

                if (!string.IsNullOrEmpty(moduleVersion) &&
                    !moduleVersion.StartsWith(VersionInfo.Version, StringComparison.Ordinal))
                {
                    DialogResult vr = MessageBox.Show(this,
                        "磁盘扩展包版本（" + moduleVersion + "）与主程序（" + VersionInfo.Version + "）不一致。\r\n" +
                        "旧版模块可能缺少最新的安全修复，建议从 Releases 重新下载 disks.zip。\r\n\r\n" +
                        "仍要启动吗？",
                        "版本不一致", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (vr != DialogResult.Yes) return;
                }

                try
                {
                    Process.Start(module);
                    return;
                }
                catch { }
            }

            string hint = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "磁盘扩展包.exe");
            DialogResult r = MessageBox.Show(this,
                "磁盘扩展包是可选模块，用来扫描 D/E 盘、U 盘和移动硬盘。\r\n\r\n" +
                "它不随主程序安装。请从 Releases 下载 c-drive-cleaner-disks.zip，\r\n" +
                "解压后把「磁盘扩展包.exe」放到程序目录：\r\n" + hint + "\r\n\r\n" +
                "是否打开下载页面？",
                "磁盘扩展包", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (r == DialogResult.Yes)
            {
                try { Process.Start("https://github.com/Misaka-11736/c-drive-cleaner/releases/latest"); }
                catch { }
            }
        }

        public ScanResult Scan
        {
            get { return _scan; }
        }

        // ------------------------------------------------------------------ UI
        private void BuildUi()
        {
            _sidebar = new Panel();
            _sidebar.Dock = DockStyle.Left;
            _sidebar.Width = Theme.S(206);
            _sidebar.BackColor = Theme.Sidebar;
            _sidebar.Paint += SidebarPaint;

            _content = new Panel();
            _content.Dock = DockStyle.Fill;
            _content.BackColor = Theme.Bg;
            _content.Padding = new Padding(Theme.S(16), Theme.S(10), Theme.S(16), Theme.S(8));

            _status = new Panel();
            _status.Dock = DockStyle.Bottom;
            _status.Height = Theme.S(46);
            _status.BackColor = Theme.Bg;
            _status.Paint += StatusPaint;

            _header = new Panel();
            _header.Dock = DockStyle.Top;
            _header.Height = Theme.S(80);
            _header.BackColor = Theme.Bg;
            _header.Paint += HeaderPaint;

            Controls.Add(_content);
            Controls.Add(_status);
            Controls.Add(_header);
            Controls.Add(_sidebar);

            _pageTitle = new Label();
            _pageTitle.AutoSize = false;
            _pageTitle.BackColor = Color.Transparent;
            _pageTitle.ForeColor = Theme.Text;
            _pageTitle.Font = Theme.F(14f, FontStyle.Bold);
            _pageTitle.TextAlign = ContentAlignment.MiddleLeft;
            _header.Controls.Add(_pageTitle);

            _pageSub = new Label();
            _pageSub.AutoSize = false;
            _pageSub.BackColor = Color.Transparent;
            _pageSub.ForeColor = Theme.Muted;
            _pageSub.Font = Theme.F(8.5f);
            _pageSub.TextAlign = ContentAlignment.MiddleLeft;
            _header.Controls.Add(_pageSub);

            _btnExport = MakeButton("导出报告", false);
            _btnExport.Click += delegate { ExportReports(); };
            _header.Controls.Add(_btnExport);

            _btnImport = MakeButton("导入计划", false);
            _btnImport.Click += delegate { ImportPlan(); };
            _header.Controls.Add(_btnImport);

            _btnScan = MakeButton("重新扫描", false);
            _btnScan.Click += delegate { BeginScan(); };
            _header.Controls.Add(_btnScan);

            _btnClean = MakeButton("开始清理", true);
            _btnClean.Click += delegate { BeginClean(); };
            _header.Controls.Add(_btnClean);

            _statusText = new Label();
            _statusText.AutoSize = false;
            _statusText.BackColor = Color.Transparent;
            _statusText.ForeColor = Theme.Muted;
            _statusText.Font = Theme.F(8.5f);
            _statusText.TextAlign = ContentAlignment.MiddleLeft;
            _status.Controls.Add(_statusText);

            _selText = new Label();
            _selText.AutoSize = false;
            _selText.BackColor = Color.Transparent;
            _selText.ForeColor = Theme.Text;
            _selText.Font = Theme.F(8.5f);
            _selText.TextAlign = ContentAlignment.MiddleRight;
            _status.Controls.Add(_selText);

            _progress = new SlimProgress();
            _progress.Value = 0;
            _status.Controls.Add(_progress);

            _btnCancel = MakeButton("取消", false);
            _btnCancel.Width = Theme.S(64);
            _btnCancel.Visible = false;
            _btnCancel.Click += delegate { CancelWork(); };
            _status.Controls.Add(_btnCancel);

            _nav = new List<NavItem>();
            string[] names = { "概览", "可清理", "仅统计", "排行", "建议", "日志" };
            for (int i = 0; i < names.Length; i++)
            {
                NavItem n = new NavItem();
                n.Text = names[i];
                n.Index = i;
                n.Visible = true;
                n.Activate += delegate(object s, EventArgs e)
                {
                    NavItem item = s as NavItem;
                    if (item != null) ShowPage(item.Index);
                };
                _sidebar.Controls.Add(n);
                _nav.Add(n);
            }

            _overview = new OverviewPage(this);
            _clean = new CleanPage(this);
            _stats = new StatsPage(this);
            _rank = new RankPage(this);
            _suggest = new SuggestionPage(this);
            _log = new LogPage(this);
            _pages = new PageBase[] { _overview, _clean, _stats, _rank, _suggest, _log };
            foreach (PageBase p in _pages) _content.Controls.Add(p);

            _banner = new UpdateBanner();
            _banner.ViewClicked += delegate
            {
                if (_updateInfo != null && _updateInfo.Url.Length > 0)
                {
                    try { Process.Start(_updateInfo.Url); } catch { }
                }
                _banner.Visible = false;
            };
            _banner.Ignored += delegate
            {
                if (_updateInfo != null) UpdateChecker.Ignore(LocalAppData, _updateInfo.Version);
            };
            _content.Controls.Add(_banner);

            _settingsLink = new Label();
            _settingsLink.Text = "设置";
            _settingsLink.AutoSize = false;
            _settingsLink.BackColor = Color.Transparent;
            _settingsLink.ForeColor = Color.FromArgb(148, 163, 184);
            _settingsLink.Font = Theme.F(8.5f);
            _settingsLink.Cursor = Cursors.Hand;
            _settingsLink.TextAlign = ContentAlignment.MiddleLeft;
            _settingsLink.Click += delegate
            {
                using (SettingsForm f = new SettingsForm(LocalAppData, VersionInfo.Version))
                    f.ShowDialog(this);
            };
            _sidebar.Controls.Add(_settingsLink);

            _diskLink = new Label();
            _diskLink.Text = "磁盘扩展包";
            _diskLink.AutoSize = false;
            _diskLink.BackColor = Color.Transparent;
            _diskLink.ForeColor = Color.FromArgb(148, 163, 184);
            _diskLink.Font = Theme.F(8.5f);
            _diskLink.Cursor = Cursors.Hand;
            _diskLink.TextAlign = ContentAlignment.MiddleLeft;
            _diskLink.Click += delegate { OpenDiskPack(); };
            _sidebar.Controls.Add(_diskLink);

            Resize += delegate { LayoutChrome(); };
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.F5) BeginScan();
                if (e.Control && e.KeyCode == Keys.E) ExportReports();
                if (e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D6)
                    ShowPage(e.KeyCode - Keys.D1);
                if (e.Control && e.KeyCode == Keys.Enter) BeginClean();
            };
            FormClosing += MainFormClosing;

            LayoutChrome();
        }

        private RoundButton MakeButton(string text, bool primary)
        {
            RoundButton b = new RoundButton();
            b.Text = text;
            b.Width = Theme.S(96);
            b.Height = Theme.S(34);
            b.Font = Theme.F(9f);
            if (primary)
            {
                b.Fill = Theme.Primary;
                b.HoverFill = Theme.PrimaryDark;
                b.TextColor = Color.White;
                b.BorderColor = Color.Transparent;
            }
            return b;
        }

        private void LayoutChrome()
        {
            _sidebar.Width = Theme.S(206);

            int logoBottom = Theme.S(96);
            for (int i = 0; i < _nav.Count; i++)
            {
                _nav[i].Location = new Point(0, logoBottom + i * Theme.S(46));
                _nav[i].Width = _sidebar.Width;
                _nav[i].Height = Theme.S(46);
            }

            if (_settingsLink != null)
            {
                _settingsLink.Location = new Point(Theme.S(22), _sidebar.Height - Theme.S(104));
                _settingsLink.Size = new Size(Theme.S(120), Theme.S(24));
            }

            if (_diskLink != null)
            {
                _diskLink.Location = new Point(Theme.S(22), _sidebar.Height - Theme.S(130));
                _diskLink.Size = new Size(Theme.S(120), Theme.S(24));
            }

            _pageTitle.Location = new Point(Theme.S(24), Theme.S(14));
            _pageTitle.Size = new Size(Theme.S(420), Theme.S(30));
            _pageSub.Location = new Point(Theme.S(25), Theme.S(45));
            _pageSub.Size = new Size(Theme.S(480), Theme.S(22));

            int right = _header.Width - Theme.S(22);
            int y = (_header.Height - Theme.S(34)) / 2;
            _btnClean.Location = new Point(right - _btnClean.Width, y);
            right -= _btnClean.Width + Theme.S(10);
            _btnScan.Location = new Point(right - _btnScan.Width, y);
            right -= _btnScan.Width + Theme.S(10);
            _btnExport.Location = new Point(right - _btnExport.Width, y);
            right -= _btnExport.Width + Theme.S(10);
            _btnImport.Location = new Point(right - _btnImport.Width, y);

            int sh = _status.Height;
            int sright = _status.Width - Theme.S(22);
            _btnCancel.Location = new Point(sright - _btnCancel.Width, (sh - Theme.S(28)) / 2);
            _btnCancel.Height = Theme.S(28);
            sright -= _btnCancel.Width + Theme.S(10);

            _selText.Size = new Size(Theme.S(190), sh);
            _selText.Location = new Point(sright - _selText.Width, 0);

            _progress.Width = Theme.S(180);
            _progress.Height = Theme.S(8);
            _progress.Location = new Point(_selText.Left - Theme.S(14) - _progress.Width, (sh - Theme.S(8)) / 2);

            _statusText.Location = new Point(Theme.S(22), 0);
            _statusText.Size = new Size(Math.Max(Theme.S(100), _progress.Left - Theme.S(34)), sh);

            _overview.Invalidate();
        }

        private void SidebarPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle logo = new Rectangle(Theme.S(20), Theme.S(20), Theme.S(40), Theme.S(40));
            using (GraphicsPath p = Theme.RoundRect(logo, Theme.S(11)))
            using (LinearGradientBrush b = new LinearGradientBrush(logo,
                Color.FromArgb(96, 165, 250), Color.FromArgb(37, 99, 235), 45f))
            {
                g.FillPath(b, p);
            }
            TextRenderer.DrawText(g, "C", Theme.F(17f, FontStyle.Bold), logo, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, "C盘清理助手", Theme.F(11f, FontStyle.Bold),
                new Rectangle(Theme.S(70), Theme.S(20), Theme.S(126), Theme.S(24)), Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "v" + AppVersion + " · Windows", Theme.F(7.5f),
                new Rectangle(Theme.S(70), Theme.S(42), Theme.S(126), Theme.S(18)),
                Color.FromArgb(148, 163, 184), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            int bottom = _sidebar.Height;
            Rectangle dot = new Rectangle(Theme.S(22), bottom - Theme.S(52), Theme.S(7), Theme.S(7));
            using (SolidBrush b = new SolidBrush(Util.IsAdmin() ? Color.FromArgb(52, 211, 153) : Theme.Amber))
                g.FillEllipse(b, dot);

            TextRenderer.DrawText(g, Util.IsAdmin() ? "管理员模式" : "普通用户（请以管理员运行）", Theme.F(8f),
                new Rectangle(Theme.S(36), bottom - Theme.S(62), Theme.S(160), Theme.S(26)),
                Util.IsAdmin() ? Color.FromArgb(148, 163, 184) : Theme.Amber,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            using (Pen pen = new Pen(Color.FromArgb(30, 41, 59)))
                g.DrawLine(pen, Theme.S(16), bottom - Theme.S(70), _sidebar.Width - Theme.S(16), bottom - Theme.S(70));
        }

        private void HeaderPaint(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(Theme.Border))
                e.Graphics.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1);
        }

        private void StatusPaint(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(Theme.Border))
                e.Graphics.DrawLine(pen, 0, 0, _status.Width, 0);
        }

        // ------------------------------------------------------------------ 导航
        public void ShowPage(int index)
        {
            if (_pages == null || index < 0 || index >= _pages.Length) return;
            _pageIndex = index;
            for (int i = 0; i < _pages.Length; i++)
                _pages[i].Visible = (i == index);
            for (int i = 0; i < _nav.Count; i++)
            {
                _nav[i].Selected = (i == index);
                _nav[i].Invalidate();
            }
            _pageTitle.Text = _titles[index];
            _pageSub.Text = _subs[index];
        }

        public void SetStatus(string text)
        {
            _statusText.Text = text;
        }

        public void UpdateSelection(int count, long bytes)
        {
            _selText.Text = string.Format("已选 {0} 项 · {1}", count, Fmt.Size(bytes));
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _btnScan.Enabled = !busy;
            _btnExport.Enabled = !busy && _scan != null;
            _btnImport.Enabled = !busy;
            _btnClean.Enabled = !busy && _scan != null;
            _btnCancel.Visible = busy;
            foreach (NavItem n in _nav) n.Enabled = !busy;
            if (!busy) _progress.Value = 0;
            foreach (PageBase p in _pages) p.OnBusyChanged(busy);
            if (!busy) LayoutChrome();
        }

        public bool IsBusy
        {
            get { return _busy; }
        }

        private void CancelWork()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                SetStatus("正在取消...");
            }
        }

        // ------------------------------------------------------------------ 扫描
        public void BeginScan()
        {
            if (_busy) return;

            _scan = null;
            _clean.ClearAll();
            _stats.ClearAll();
            _rank.ClearAll();
            _suggest.ClearAll();
            _overview.Invalidate();
            UpdateSelection(0, 0);

            _cts = new CancellationTokenSource();
            _workWatch.Restart();
            SetBusy(true);
            _progress.Value = -1;
            SetStatus("正在扫描 C 盘，只读不写...");

            Scanner scanner = new Scanner(UserProfile, LocalAppData, RoamingAppData);
            Progress<ScanProgress> progress = new Progress<ScanProgress>(delegate(ScanProgress p)
            {
                if (!_busy) return;
                SetStatus(string.Format("扫描中  {0} 文件 / {1} 目录 / {2} · 已用 {3}",
                    Fmt.Count(p.Files), Fmt.Count(p.Dirs), Fmt.Size(p.Bytes), Fmt.Duration(_workWatch.Elapsed)));
            });
            CancellationToken token = _cts.Token;

            Thread th = new Thread(delegate()
            {
                try
                {
                    ScanResult r = scanner.Scan(progress, token);
                    BeginInvoke(new Action(delegate() { ScanDone(r); }));
                }
                catch (OperationCanceledException)
                {
                    BeginInvoke(new Action(delegate() { ScanCanceled(); }));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(delegate() { ScanFailed(ex); }));
                }
            });
            th.IsBackground = true;
            th.Start();
        }

        private void ScanDone(ScanResult r)
        {
            _scan = r;
            SetBusy(false);
            _progress.Value = 0;
            _overview.OnScan(r);
            _clean.OnScan(r);
            _stats.OnScan(r);
            _rank.OnScan(r);
            _suggest.OnScan(r);
            SetStatus(string.Format("扫描完成：{0} 个文件、{1} 个目录，用时 {2} 秒",
                Fmt.Count(r.TotalFiles), Fmt.Count(r.DirCount), (int)r.DurationSec));
            _clean.SelectRecommended();
            ShowPage(_startPage);
        }

        private void ScanCanceled()
        {
            SetBusy(false);
            SetStatus("扫描已取消");
        }

        private void ScanFailed(Exception ex)
        {
            SetBusy(false);
            SetStatus("扫描失败");
            MessageBox.Show(this, "扫描失败：\r\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // ------------------------------------------------------------------ 清理
        /// <summary>对当前选中的项目执行一次演练（不删除）。</summary>
        public void DryRunPlan()
        {
            if (_busy || _scan == null) return;
            List<CleanTarget> items = _clean.SelectedTargets();
            if (items.Count == 0) return;
            try
            {
                Cleaner.Validate(items);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "安全校验未通过", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            RunClean(items, true);
        }

        public void BeginClean()
        {
            if (_busy) return;
            if (_scan == null)
            {
                MessageBox.Show(this, "请先扫描 C 盘。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<CleanTarget> items = _clean.SelectedTargets();
            if (items.Count == 0)
            {
                MessageBox.Show(this, "还没有勾选任何项目。\r\n先在「可清理」页勾选要删除的内容。", AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                ShowPage(1);
                return;
            }

            try
            {
                Cleaner.Validate(items);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "安全校验未通过", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            long total = 0;
            foreach (CleanTarget t in items) total += t.Bytes;

            ConfirmDialog dlg = new ConfirmDialog(items, total);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            RunClean(items, dlg.DryRun);
        }

        private void RunClean(List<CleanTarget> items, bool dryRun)
        {
            _cts = new CancellationTokenSource();
            _workWatch.Restart();
            SetBusy(true);
            _progress.Value = 0;
            SetStatus(dryRun ? "演练中（不会删除文件）..." : "正在清理...");

            Cleaner cleaner = new Cleaner(OutDir, UserProfile);
            Progress<CleanProgress> progress = new Progress<CleanProgress>(delegate(CleanProgress p)
            {
                if (!_busy) return;
                if (p.Total > 0) _progress.Value = (int)(p.Index * 100.0 / p.Total);
                SetStatus(string.Format("清理中 {0}/{1} · 已用 {2} · {3}",
                    p.Index, p.Total, Fmt.Duration(_workWatch.Elapsed), p.Message));
            });
            CancellationToken token = _cts.Token;

            Thread th = new Thread(delegate()
            {
                try
                {
                    CleanResult r = cleaner.Clean(items, dryRun, progress, token);
                    BeginInvoke(new Action(delegate() { CleanDone(r, dryRun); }));
                }
                catch (OperationCanceledException)
                {
                    BeginInvoke(new Action(delegate()
                    {
                        SetBusy(false);
                        SetStatus("清理已取消");
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(delegate()
                    {
                        SetBusy(false);
                        SetStatus("清理失败");
                        MessageBox.Show(this, "清理失败：\r\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
            th.IsBackground = true;
            th.Start();
        }

        private void CleanDone(CleanResult r, bool dryRun)
        {
            SetBusy(false);
            _progress.Value = 0;

            if (_scan != null)
            {
                _scan.DriveFree = Util.FreeSpace("C");
                _scan.DriveUsed = _scan.DriveTotal - _scan.DriveFree;
                _overview.OnScan(_scan);
            }

            if (r.LogPath.Length > 0) _log.SetLogFile(r.LogPath);

            if (dryRun)
            {
                SetStatus("演练完成，未删除任何文件");
                MessageBox.Show(this,
                    "演练完成，没有删除任何文件。\r\n\r\n计划释放：" + Fmt.Size(r.BeforeFree) + " 不变，共 " + "已检查项目" + "\r\n日志：" + r.LogPath,
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                ShowPage(5);
                return;
            }

            if (r.Canceled) SetStatus("清理已取消，部分项目已处理");
            else SetStatus(string.Format("清理完成，实际释放 {0}", Fmt.Size(r.Freed)));

            string msg = string.Format(
                "{0}\r\n\r\nC 盘可用：{1} → {2}\r\n实际释放：{3}\r\n未能删除：{4} 项{5}\r\n\r\n日志：{6}",
                r.Canceled ? "清理已取消（部分项目已删除）" : "清理完成",
                Fmt.Size(r.BeforeFree), Fmt.Size(r.AfterFree), Fmt.Size(r.Freed), r.Errors,
                r.Errors > 0 ? "（多为占用中的文件，详见日志）" : "",
                r.LogPath);

            MessageBox.Show(this, msg, AppName, MessageBoxButtons.OK,
                r.Errors > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            ShowPage(5);
        }

        // ------------------------------------------------------------------ 导出 / 导入
        public void ExportReports()
        {
            if (_scan == null)
            {
                MessageBox.Show(this, "请先扫描 C 盘，再导出报告。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string plan = Path.Combine(OutDir, "C-Clean-Plan-" + stamp + ".json");
                string html = Path.Combine(OutDir, "C-Clean-Report-" + stamp + ".html");

                PlanStore.Save(_scan, plan);
                HtmlReport.Save(_scan, html);
                _scan.PlanPath = plan;
                _scan.HtmlPath = html;

                SetStatus("报告已导出：" + html);
                try { Process.Start(html); } catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出失败：\r\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ImportPlan()
        {
            if (_busy) return;
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "导入清理计划";
            dlg.Filter = "清理计划 (*.json)|*.json|所有文件 (*.*)|*.*";
            dlg.InitialDirectory = Directory.Exists(OutDir) ? OutDir : UserProfile;
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            LoadPlan(dlg.FileName);
        }

        public void LoadPlan(string path)
        {
            try
            {
                List<CleanTarget> list = PlanStore.Load(path);
                ScanResult r = BuildResultFromPlan(list);
                _scan = r;
                _overview.OnScan(r);
                _clean.OnScan(r);
                _stats.OnScan(r);
                _rank.OnScan(r);
                _suggest.OnScan(r);
                _clean.SelectRecommended();
                SetStatus("已导入计划：" + Path.GetFileName(path));
                ShowPage(1);
                SetBusy(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导入失败：\r\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private ScanResult BuildResultFromPlan(List<CleanTarget> list)
        {
            ScanResult r = new ScanResult();
            r.GeneratedAt = DateTime.Now;
            r.Elevated = Util.IsAdmin();
            r.DriveTotal = Util.TotalSpace("C");
            r.DriveFree = Util.FreeSpace("C");
            r.DriveUsed = r.DriveTotal - r.DriveFree;
            r.Targets = list;

            List<CategoryDef> defs = TargetCatalog.CreateCategories();
            foreach (CategoryDef def in defs)
            {
                CategoryResult cr = new CategoryResult();
                cr.Def = def;
                foreach (CleanTarget t in list) if (t.Cat == def.Id) cr.Items.Add(t);
                foreach (CleanTarget t in cr.Items)
                {
                    if (t.Deletable)
                    {
                        cr.CleanBytes += t.Bytes;
                        if (t.Recommend) cr.RecBytes += t.Bytes;
                    }
                    else cr.ReportBytes += t.Bytes;
                }
                r.CleanBytes += cr.CleanBytes;
                r.RecBytes += cr.RecBytes;
                r.ReportBytes += cr.ReportBytes;
                r.Categories.Add(cr);
            }
            r.Suggestions.Add(new Suggestion() { Title = "已导入清理计划", Body = "这份数据来自计划文件，不含目录排行。建议重新扫描以获得最新占用。", Bytes = 0 });
            return r;
        }

        public void OpenOutDir()
        {
            try
            {
                if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);
                Process.Start("explorer.exe", "\"" + OutDir + "\"");
            }
            catch { }
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_busy)
            {
                DialogResult r = MessageBox.Show(this, "任务正在运行，确定要退出吗？", AppName,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) e.Cancel = true;
                else if (_cts != null) _cts.Cancel();
            }
        }
    }
}
