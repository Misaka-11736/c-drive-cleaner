using System;
using System.Windows.Forms;

namespace CCleaner
{
    public class PageBase : UserControl
    {
        protected MainForm Host;

        public PageBase(MainForm host)
        {
            Host = host;
            Dock = DockStyle.Fill;
            BackColor = Theme.Bg;
            AutoScaleMode = AutoScaleMode.None;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        public virtual void OnScan(ScanResult r) { }
        public virtual void OnBusyChanged(bool busy) { }
    }
}
