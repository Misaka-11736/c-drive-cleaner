using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CCleaner
{
    public class LogPage : PageBase
    {
        private TextBox _box;
        private Label _pathLabel;
        private string _path = "";

        public LogPage(MainForm host) : base(host)
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(Theme.S(14), Theme.S(10), Theme.S(14), Theme.S(12));

            Panel toolbar = new Panel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = Theme.S(46);
            toolbar.BackColor = Color.White;

            RoundButton open = new RoundButton();
            open.Text = "打开输出目录";
            open.Width = Theme.S(110);
            open.Height = Theme.S(30);
            open.Location = new Point(0, Theme.S(4));
            open.Click += delegate { Host.OpenOutDir(); };
            toolbar.Controls.Add(open);

            RoundButton export = new RoundButton();
            export.Text = "导出本次报告";
            export.Width = Theme.S(110);
            export.Height = Theme.S(30);
            export.Location = new Point(Theme.S(120), Theme.S(4));
            export.Click += delegate { Host.ExportReports(); };
            toolbar.Controls.Add(export);

            _pathLabel = new Label();
            _pathLabel.AutoSize = false;
            _pathLabel.BackColor = Color.White;
            _pathLabel.ForeColor = Theme.Subtle;
            _pathLabel.Font = Theme.F(8f);
            _pathLabel.TextAlign = ContentAlignment.MiddleRight;
            _pathLabel.Height = Theme.S(30);
            _pathLabel.Location = new Point(Theme.S(240), Theme.S(4));
            _pathLabel.Text = "尚无清理记录";
            toolbar.Controls.Add(_pathLabel);

            toolbar.Resize += delegate
            {
                _pathLabel.Location = new Point(Theme.S(240), Theme.S(4));
                _pathLabel.Width = Math.Max(Theme.S(80), toolbar.Width - Theme.S(240));
            };

            _box = new TextBox();
            _box.Multiline = true;
            _box.ReadOnly = true;
            _box.ScrollBars = ScrollBars.Both;
            _box.WordWrap = false;
            _box.BorderStyle = BorderStyle.None;
            _box.BackColor = Color.White;
            _box.ForeColor = Theme.Text;
            _box.Font = new Font("Consolas", 9f, FontStyle.Regular, GraphicsUnit.Point);
            _box.Dock = DockStyle.Fill;
            _box.Text = "这里显示清理日志。\r\n\r\n执行一次清理后，会记录每一项的删除结果、未删除的文件以及最终释放的空间。";

            card.Controls.Add(_box);
            card.Controls.Add(toolbar);
            Controls.Add(card);
        }

        public void SetLogFile(string path)
        {
            _path = path;
            _pathLabel.Text = path;
            try
            {
                _box.Text = File.ReadAllText(path, Encoding.UTF8);
                _box.SelectionStart = _box.TextLength;
                _box.ScrollToCaret();
            }
            catch (Exception ex)
            {
                _box.Text = "读取日志失败: " + ex.Message;
            }
        }

        public string LogPath
        {
            get { return _path; }
        }
    }
}
