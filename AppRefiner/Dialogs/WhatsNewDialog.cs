using System.Runtime.InteropServices;

namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Dialog for displaying What's New information on first run of a new version
    /// </summary>
    public class WhatsNewDialog : Form
    {
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly RichTextBox contentTextBox;
        private readonly CheckBox dontShowAgainCheckBox;
        private readonly Button okButton;
        private readonly IntPtr owner;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;

        /// <summary>
        /// Gets whether the user checked "Don't show this again"
        /// </summary>
        public bool DontShowAgain => dontShowAgainCheckBox.Checked;

        /// <summary>
        /// Initializes a new instance of the WhatsNewDialog class
        /// </summary>
        /// <param name="version">The current version of AppRefiner</param>
        /// <param name="owner">The owner window handle</param>
        public WhatsNewDialog(string version, IntPtr owner = default)
        {
            this.owner = owner;
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.contentTextBox = new RichTextBox();
            this.dontShowAgainCheckBox = new CheckBox();
            this.okButton = new Button();

            InitializeComponent(version);
            LoadWhatsNewContent();
        }

        private void InitializeComponent(string version)
        {
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();

            // headerPanel
            this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 30;
            this.headerPanel.TabIndex = 0;
            this.headerPanel.Controls.Add(this.headerLabel);

            // headerLabel
            this.headerLabel.Text = $"Welcome to AppRefiner v{version} - What's New";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TabIndex = 0;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // contentTextBox
            this.contentTextBox.BorderStyle = BorderStyle.FixedSingle;
            this.contentTextBox.Location = new Point(20, 50);
            this.contentTextBox.Size = new Size(660, 420);
            this.contentTextBox.TabIndex = 1;
            this.contentTextBox.ReadOnly = true;
            this.contentTextBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.contentTextBox.BackColor = Color.White;
            this.contentTextBox.WordWrap = true;
            this.contentTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;

            // dontShowAgainCheckBox
            this.dontShowAgainCheckBox.Text = "Don't show this again";
            this.dontShowAgainCheckBox.Location = new Point(20, 480);
            this.dontShowAgainCheckBox.Size = new Size(200, 24);
            this.dontShowAgainCheckBox.TabIndex = 2;
            this.dontShowAgainCheckBox.ForeColor = Color.Black;
            this.dontShowAgainCheckBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            // okButton
            this.okButton.Text = "OK";
            this.okButton.Size = new Size(100, 30);
            this.okButton.Location = new Point(590, 480);
            this.okButton.TabIndex = 3;
            this.okButton.BackColor = Color.FromArgb(0, 120, 215);
            this.okButton.ForeColor = Color.White;
            this.okButton.FlatStyle = FlatStyle.Flat;
            this.okButton.FlatAppearance.BorderSize = 0;
            this.okButton.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            // WhatsNewDialog
            this.Text = "What's New";
            this.ClientSize = new Size(700, 530);
            this.Controls.Add(this.headerPanel);
            this.Controls.Add(this.contentTextBox);
            this.Controls.Add(this.dontShowAgainCheckBox);
            this.Controls.Add(this.okButton);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(240, 240, 245);
            this.Padding = new Padding(1);
            this.AcceptButton = this.okButton;

            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void LoadWhatsNewContent()
        {
            // Get the directory where the executable is located
            var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var whatsNewPath = Path.Combine(exeDirectory, "whats-new.txt");

            // Read and display content (file existence already validated by caller)
            var content = File.ReadAllText(whatsNewPath);
            this.contentTextBox.Text = content;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Center on owner window
            if (owner != IntPtr.Zero)
            {
                WindowHelper.CenterFormOnWindow(this, owner);
            }
            else
            {
                this.CenterToScreen();
            }

            // Create the mouse handler if this is a modal dialog
            if (this.Modal && owner != IntPtr.Zero)
            {
                mouseHandler = new DialogHelper.ModalDialogMouseHandler(this, headerPanel, owner);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Draw a border around the form
            using var pen = new Pen(Color.FromArgb(100, 100, 120));
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            // Dispose the mouse handler
            mouseHandler?.Dispose();
            mouseHandler = null;
        }
    }
}
