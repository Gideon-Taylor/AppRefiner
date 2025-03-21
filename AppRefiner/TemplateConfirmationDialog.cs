using System;
using System.Drawing;
using System.Windows.Forms;

namespace AppRefiner
{
    public class TemplateConfirmationDialog : Form
    {
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly Label messageLabel;
        private readonly Button yesButton;
        private readonly Button noButton;
        private readonly IntPtr owner;

        public TemplateConfirmationDialog(string message, IntPtr owner = default)
        {
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.messageLabel = new Label();
            this.yesButton = new Button();
            this.noButton = new Button();
            this.owner = owner;
            
            InitializeComponent(message);
        }

        private void InitializeComponent(string message)
        {
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();

            // headerPanel
            this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 30;
            this.headerPanel.Controls.Add(this.headerLabel);

            // headerLabel
            this.headerLabel.Text = "Confirm Template Application";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // messageLabel
            this.messageLabel.Text = message;
            this.messageLabel.Location = new Point(20, 50);
            this.messageLabel.Size = new Size(360, 60);
            this.messageLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.messageLabel.AutoSize = false;

            // yesButton
            this.yesButton.Text = "Yes";
            this.yesButton.Size = new Size(100, 30);
            this.yesButton.Location = new Point(70, 120);
            this.yesButton.Click += (s, e) => 
            {
                this.DialogResult = DialogResult.Yes;
                this.Close();
            };

            // noButton
            this.noButton.Text = "No";
            this.noButton.Size = new Size(100, 30);
            this.noButton.Location = new Point(230, 120);
            this.noButton.Click += (s, e) => 
            {
                this.DialogResult = DialogResult.No;
                this.Close();
            };

            // TemplateConfirmationDialog
            this.Text = "Confirm";
            this.ClientSize = new Size(400, 170);
            this.Controls.Add(this.messageLabel);
            this.Controls.Add(this.yesButton);
            this.Controls.Add(this.noButton);
            this.Controls.Add(this.headerPanel);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.AcceptButton = this.yesButton;
            this.CancelButton = this.noButton;

            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            
            // Center on owner window
            if (owner != IntPtr.Zero)
            {
                WindowHelper.CenterFormOnWindow(this, owner);
            }
        }
    }
}
