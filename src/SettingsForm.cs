using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CCleaner
{
    public class SettingsForm : Form
    {
        private readonly string _localAppData;
        private readonly string _currentVersion;
        private CheckBox _auto;
        private RoundButton _check;
        private Label _result;

        public SettingsForm(string localAppData, string currentVersion)
        {
            _localAppData = localAppData;
            _currentVersion = currentVersion;

            Text = "设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.White;
            Font = Theme.F(9f);
            ClientSize = new Size(Theme.S(520), Theme.S(340));

            Label title = new Label();
            title.Text = "设置";
            title.Font = Theme.F(15f, FontStyle.Bold);
            title.ForeColor = Theme.Text;
            title.AutoSize = false;
            title.Location = new Point(Theme.S(28), Theme.S(22));
            title.Size = new Size(Theme.S(300), Theme.S(32));
            Controls.Add(title);

            Label ver = new Label();
            ver.Text = "C盘清理助手 v" + _currentVersion;
            ver.ForeColor = Theme.Muted;
            ver.Font = Theme.F(9f);
            ver.AutoSize = false;
            ver.Location = new Point(Theme.S(29), Theme.S(56));
            ver.Size = new Size(Theme.S(400), Theme.S(22));
            Controls.Add(ver);

            _auto = new CheckBox();
            _auto.Text = "启动时自动检查更新（每天最多一次）";
            _auto.ForeColor = Theme.Text;
            _auto.Font = Theme.F(9f);
            _auto.AutoSize = true;
            _auto.Location = new Point(Theme.S(28), Theme.S(98));
            Controls.Add(_auto);

            Label note = new Label();
            note.Text = "检查只访问 GitHub Releases 页面，不发送任何个人信息；\r\n" +
                        "发现新版本时只在窗口顶部提示，不会自动下载或安装。";
            note.ForeColor = Theme.Subtle;
            note.Font = Theme.F(8.5f);
            note.AutoSize = false;
            note.Location = new Point(Theme.S(28), Theme.S(130));
            note.Size = new Size(Theme.S(460), Theme.S(46));
            Controls.Add(note);

            _check = new RoundButton();
            _check.Text = "立即检查更新";
            _check.Width = Theme.S(116);
            _check.Height = Theme.S(32);
            _check.Location = new Point(Theme.S(28), Theme.S(190));
            _check.Click += delegate { CheckNow(); };
            Controls.Add(_check);

            _result = new Label();
            _result.Text = "";
            _result.ForeColor = Theme.Muted;
            _result.Font = Theme.F(8.5f);
            _result.AutoSize = false;
            _result.Location = new Point(Theme.S(154), Theme.S(190));
            _result.Size = new Size(Theme.S(340), Theme.S(32));
            _result.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(_result);

            string module = FindModule();
            string moduleText = "磁盘扩展包：未安装（可选）";
            if (!string.IsNullOrEmpty(module))
            {
                string mv = "";
                try { mv = FileVersionInfo.GetVersionInfo(module).FileVersion; } catch { }
                moduleText = "磁盘扩展包：已安装" + (string.IsNullOrEmpty(mv) ? "" : " v" + mv);
            }

            Label modLabel = new Label();
            modLabel.Text = moduleText;
            modLabel.ForeColor = Theme.Muted;
            modLabel.Font = Theme.F(8.5f);
            modLabel.AutoSize = false;
            modLabel.Location = new Point(Theme.S(28), Theme.S(232));
            modLabel.Size = new Size(Theme.S(300), Theme.S(22));
            modLabel.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(modLabel);

            RoundButton modBtn = new RoundButton();
            modBtn.Text = "打开下载页";
            modBtn.Width = Theme.S(116);
            modBtn.Height = Theme.S(30);
            modBtn.Location = new Point(ClientSize.Width - Theme.S(28) - modBtn.Width, Theme.S(228));
            modBtn.Click += delegate
            {
                try { Process.Start("https://github.com/Misaka-11736/c-drive-cleaner/releases/latest"); }
                catch { }
            };
            Controls.Add(modBtn);

            RoundButton cancel = new RoundButton();
            cancel.Text = "取消";
            cancel.Width = Theme.S(84);
            cancel.Height = Theme.S(34);
            cancel.Location = new Point(ClientSize.Width - Theme.S(28) - cancel.Width, ClientSize.Height - Theme.S(56));
            cancel.Click += delegate { Close(); };
            Controls.Add(cancel);

            RoundButton save = new RoundButton();
            save.Text = "保存";
            save.Width = Theme.S(96);
            save.Height = Theme.S(34);
            save.Fill = Theme.Primary;
            save.HoverFill = Theme.PrimaryDark;
            save.TextColor = Color.White;
            save.BorderColor = Color.Transparent;
            save.Location = new Point(cancel.Left - Theme.S(10) - save.Width, cancel.Top);
            save.Click += delegate { SaveSettings(); };
            Controls.Add(save);

            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };

            UpdateState state = UpdateStore.Load(_localAppData);
            _auto.Checked = state.AutoCheck;
        }

        private void SaveSettings()
        {
            UpdateState state = UpdateStore.Load(_localAppData);
            state.AutoCheck = _auto.Checked;
            UpdateStore.Save(_localAppData, state);
            Close();
        }

        private string FindModule()
        {
            string[] candidates = new string[]
            {
                Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "磁盘扩展包.exe"),
                Path.Combine(_localAppData, "C盘清理助手", "dlc", "磁盘扩展包.exe")
            };
            foreach (string p in candidates)
            {
                try { if (File.Exists(p)) return p; }
                catch { }
            }
            return null;
        }

        private void CheckNow()
        {
            _check.Enabled = false;
            _result.Text = "检查中...";

            Thread t = new Thread(delegate()
            {
                UpdateInfo info = UpdateChecker.Fetch();
                try
                {
                    BeginInvoke(new Action(delegate()
                    {
                        _check.Enabled = true;

                        if (info == null)
                        {
                            _result.Text = "检查失败：网络不可用或 GitHub 无法访问";
                            return;
                        }
                        if (!UpdateChecker.IsNewer(info.Version, _currentVersion))
                        {
                            _result.Text = "当前已是最新版本（v" + _currentVersion + "）";
                            return;
                        }

                        _result.Text = "发现新版本 " + info.Version;
                        DialogResult r = MessageBox.Show(this,
                            "发现新版本 " + info.Version + "（当前 " + _currentVersion + "）。\r\n\r\n是否打开下载页面？",
                            "检查更新", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (r == DialogResult.Yes)
                        {
                            try { Process.Start(info.Url); } catch { }
                        }
                    }));
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }
    }
}
