using System;
using System.Drawing;
using System.Windows.Forms;

namespace AppRefiner
{
    public class CommandProgressDialog : Form
    {
        private Panel headerPanel;
        private Label headerLabel;
        private ProgressBar progressBar;

        public CommandProgressDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.progressBar = new ProgressBar();
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();
            
            // headerPanel
            this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 30;
            this.headerPanel.Controls.Add(this.headerLabel);
            
            // headerLabel
            this.headerLabel.Text = "Executing Command...";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // progressBar
            this.progressBar.Dock = DockStyle.Fill;
            this.progressBar.Style = ProgressBarStyle.Marquee;
            this.progressBar.MarqueeAnimationSpeed = 30;
            this.progressBar.Location = new Point(0, 30);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new Size(300, 20);
            
            // CommandProgressDialog
            this.ClientSize = new Size(300, 70);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.headerPanel);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Name = "CommandProgressDialog";
            this.Text = "Executing Command";
            this.ShowInTaskbar = false;
            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                // Add drop shadow effect to the form
                const int CS_DROPSHADOW = 0x00020000;
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }
    }
}
