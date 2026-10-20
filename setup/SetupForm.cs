using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CCleaner.Setup
{
    public class SetupForm : Form
    {
        private readonly bool _uninstall;
        private RoundButton _action;
        private CheckBox _desktop;
        private CheckBox _launch;

        public SetupForm(bool uninstall)
        {
            _uninstall = uninstall;

            Text = (uninstall ? "卸载 " : "安装 ") + Installer.AppName;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = true;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.White;
            Font = Theme.F(9f);
            ClientSize = new Size(Theme.S(660), Theme.S(436));

            GradientPanel hero = new GradientPanel();
            hero.Dock = DockStyle.Top;
            hero.Height = Theme.S(108);
            hero.Radius = 0;
            hero.Paint += HeroPaint;
            Controls.Add(hero);

            int left = Theme.S(34);
            int width = ClientSize.Width - Theme.S(68);
            int y = Theme.S(128);

            Label caption = new Label();
            caption.Text = uninstall ? "将从以下位置移除程序、快捷方式和卸载项：" : "将安装到：";
            caption.ForeColor = Theme.Muted;
            caption.Font = Theme.F(8.5f);
            caption.AutoSize = false;
            caption.Location = new Point(left, y);
            caption.Size = new Size(width, Theme.S(22));
            Controls.Add(caption);

            Label path = new Label();
            path.Text = uninstall ? Installer.InstalledDir() : Installer.DefaultDir;
            path.ForeColor = Theme.Text;
            path.Font = Theme.F(10f, FontStyle.Bold);
            path.AutoSize = false;
            path.Location = new Point(left, y + Theme.S(22));
            path.Size = new Size(width, Theme.S(48));
            Controls.Add(path);

            if (!uninstall)
            {
                _desktop = new CheckBox();
                _desktop.Text = "创建桌面快捷方式";
                _desktop.Checked = true;
                _desktop.ForeColor = Theme.Text;
                _desktop.Font = Theme.F(9f);
                _desktop.AutoSize = true;
                _desktop.Location = new Point(left, y + Theme.S(78));
                Controls.Add(_desktop);

                _launch = new CheckBox();
                _launch.Text = "安装完成后启动程序";
                _launch.Checked = true;
                _launch.ForeColor = Theme.Text;
                _launch.Font = Theme.F(9f);
                _launch.AutoSize = true;
                _launch.Location = new Point(left, y + Theme.S(106));
                Controls.Add(_launch);
            }

            string noteText;
            if (uninstall)
            {
                noteText = "扫描报告和清理日志保存在「文档\\C盘清理助手」，卸载不会删除它们。\r\n" +
                           "程序文件会在卸载完成后几秒内被自动移除。";
            }
            else
            {
                noteText = "安装不需要管理员权限。\r\n" +
                           "运行程序执行扫描/清理时，才会请求管理员权限（UAC）。\r\n" +
                           "卸载可在「设置 → 应用 → 已安装的应用」中找到。";
            }

            Label note = new Label();
            note.Text = noteText;
            note.ForeColor = Theme.Subtle;
            note.Font = Theme.F(8.5f);
            note.AutoSize = false;
            note.Location = new Point(left, y + Theme.S(146));
            note.Size = new Size(width, Theme.S(76));
            Controls.Add(note);

            if (!uninstall && Installer.IsInstalled())
            {
                Label warn = new Label();
                warn.Text = "已检测到已安装的版本，继续将覆盖安装。";
                warn.ForeColor = Theme.Amber;
                warn.Font = Theme.F(8.5f);
                warn.AutoSize = false;
                warn.Location = new Point(left, y + Theme.S(220));
                warn.Size = new Size(width, Theme.S(22));
                Controls.Add(warn);
            }

            int buttonY = ClientSize.Height - Theme.S(60);

            RoundButton cancel = new RoundButton();
            cancel.Text = "取消";
            cancel.Width = Theme.S(88);
            cancel.Height = Theme.S(36);
            cancel.Location = new Point(ClientSize.Width - Theme.S(34) - cancel.Width, buttonY);
            cancel.Click += delegate { Close(); };
            Controls.Add(cancel);

            _action = new RoundButton();
            _action.Text = uninstall ? "卸载" : (Installer.IsInstalled() ? "重新安装" : "安装");
            _action.Width = Theme.S(112);
            _action.Height = Theme.S(36);
            _action.Fill = uninstall ? Color.FromArgb(180, 35, 24) : Theme.Primary;
            _action.HoverFill = uninstall ? Color.FromArgb(140, 25, 17) : Theme.PrimaryDark;
            _action.TextColor = Color.White;
            _action.BorderColor = Color.Transparent;
            _action.Location = new Point(cancel.Left - Theme.S(10) - _action.Width, buttonY);
            _action.Click += delegate { DoAction(); };
            Controls.Add(_action);

            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) Close();
            };
        }

        private void HeroPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle logo = new Rectangle(Theme.S(32), Theme.S(30), Theme.S(46), Theme.S(46));
            Theme.FillRound(g, logo, Theme.S(12), Color.White);

            Rectangle lr = new Rectangle(logo.X, logo.Y - Theme.S(2), logo.Width, logo.Height);
            TextRenderer.DrawText(g, "C", Theme.F(20f, FontStyle.Bold), lr, Theme.Primary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, Installer.AppName, Theme.F(17f, FontStyle.Bold),
                new Rectangle(Theme.S(92), Theme.S(26), ClientSize.Width - Theme.S(120), Theme.S(32)),
                Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g,
                _uninstall ? "卸载程序 · v" + Installer.Version : "安装程序 · v" + Installer.Version,
                Theme.F(9f),
                new Rectangle(Theme.S(94), Theme.S(58), ClientSize.Width - Theme.S(120), Theme.S(24)),
                Color.FromArgb(199, 210, 254), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private void DoAction()
        {
            if (_uninstall)
            {
                DialogResult r = MessageBox.Show(
                    "确定要卸载 " + Installer.AppName + " 吗？\r\n\r\n程序文件、快捷方式和卸载项都会被移除。",
                    "卸载确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
            }

            _action.Enabled = false;
            try
            {
                if (_uninstall)
                {
                    Installer.Uninstall(false);
                    MessageBox.Show("卸载完成。\r\n\r\n程序文件会在几秒内被移除。", Installer.AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    Installer.Install(Installer.DefaultDir, _desktop.Checked, _launch.Checked, false);
                    MessageBox.Show("安装完成。\r\n\r\n可以从开始菜单或桌面快捷方式启动。", Installer.AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                Close();
            }
            catch (OperationCanceledException)
            {
                _action.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show((_uninstall ? "卸载失败：" : "安装失败：") + "\r\n" + ex.Message,
                    Installer.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                _action.Enabled = true;
            }
        }
    }
}
