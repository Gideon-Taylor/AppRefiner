namespace AppRefiner.Dialogs
{
    public class EnhancedEditorDisclaimerDialog : Form
    {
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly Label messageLabel;
        private readonly Button acceptButton;
        private readonly Button declineButton;
        private readonly IntPtr owner;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;

        public EnhancedEditorDisclaimerDialog(IntPtr owner = default)
        {
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.messageLabel = new Label();
            this.acceptButton = new Button();
            this.declineButton = new Button();
            this.owner = owner;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();

            // headerPanel
            this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 30;
            this.headerPanel.Controls.Add(this.headerLabel);

            // headerLabel
            this.headerLabel.Text = "Enhanced Editor Notice";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // messageLabel
            this.messageLabel.Text =
                "Enabling this option will cause AppRefiner to load a modified version of the editor " +
                "library into Application Designer at runtime. This modified editor is built specifically " +
                "for AppRefiner and is required to support the \"Inline Parameter Hints\" feature — " +
                "that feature cannot function without it." +
                Environment.NewLine + Environment.NewLine +
                "Your installed Application Designer files are not modified in any way. Running " +
                "Application Designer without AppRefiner, or with this option disabled, will " +
                "use the original editor library. This is a temporary, " +
                "runtime-only change." +
                Environment.NewLine + Environment.NewLine +
                "Please restart Application Designer after enabling this setting.";
            this.messageLabel.Location = new Point(20, 45);
            this.messageLabel.Size = new Size(460, 190);
            this.messageLabel.TextAlign = ContentAlignment.TopLeft;
            this.messageLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            // declineButton
            this.declineButton.Text = "Decline";
            this.declineButton.Size = new Size(100, 30);
            this.declineButton.Location = new Point(270, 215);
            this.declineButton.DialogResult = DialogResult.Cancel;
            this.declineButton.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            // acceptButton
            this.acceptButton.Text = "Accept";
            this.acceptButton.Size = new Size(100, 30);
            this.acceptButton.Location = new Point(380, 215);
            this.acceptButton.DialogResult = DialogResult.OK;
            this.acceptButton.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            // EnhancedEditorDisclaimerDialog
            this.Text = "Enhanced Editor Notice";
            this.ClientSize = new Size(500, 260);
            this.Controls.Add(this.acceptButton);
            this.Controls.Add(this.declineButton);
            this.Controls.Add(this.messageLabel);
            this.Controls.Add(this.headerPanel);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.AcceptButton = this.acceptButton;
            this.CancelButton = this.declineButton;

            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (owner != IntPtr.Zero)
            {
                WindowHelper.CenterFormOnWindow(this, owner);
            }

            mouseHandler = new DialogHelper.ModalDialogMouseHandler(this, headerPanel, owner);
        }
    }
}
