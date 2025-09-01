namespace AppRefiner.Dialogs
{
    public class MessageBoxDialog : Form
    {
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly Label messageLabel;
        private readonly FlowLayoutPanel buttonsPanel; // To hold dynamic buttons
        private readonly IntPtr owner;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;

        // Store the buttons for AcceptButton/CancelButton logic
        private Button? okButton;
        private Button? yesButton;
        private Button? noButton;
        private Button? cancelButton;
        // Abort, Retry, Ignore can be added later if needed
        private readonly Action<DialogResult>? resultCallback;

        public MessageBoxDialog(string text, string caption, MessageBoxButtons buttons, IntPtr owner = default, Action<DialogResult>? callback = null)
        {
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.messageLabel = new Label();
            this.buttonsPanel = new FlowLayoutPanel();
            this.owner = owner;
            this.resultCallback = callback;

            InitializeComponent(text, caption, buttons);
        }

        private void InitializeComponent(string text, string caption, MessageBoxButtons buttons)
        {
            this.headerPanel.SuspendLayout();
            this.buttonsPanel.SuspendLayout();
            this.SuspendLayout();

            // headerPanel
            this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 30;
            this.headerPanel.Controls.Add(this.headerLabel);

            // headerLabel
            this.headerLabel.Text = caption;
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // messageLabel
            this.messageLabel.Text = text;
            this.messageLabel.Location = new Point(20, 50); // Y position might need adjustment
            this.messageLabel.AutoSize = true; // Adjust size based on text
            this.messageLabel.MaximumSize = new Size(360, 0); // Max width, height adjusts
            this.messageLabel.TextAlign = ContentAlignment.MiddleLeft;


            // buttonsPanel
            this.buttonsPanel.Dock = DockStyle.Bottom;
            this.buttonsPanel.FlowDirection = FlowDirection.RightToLeft; // Buttons align to the right
            this.buttonsPanel.Height = 50; // Adjust as needed
            this.buttonsPanel.Padding = new Padding(10);


            // Add buttons based on MessageBoxButtons
            AddButtons(buttons);

            // Calculate dialog size
            // Initial width, can be adjusted
            int dialogWidth = 400;
            // Calculate height based on messageLabel and buttonsPanel
            // Ensure messageLabel has its size calculated first
            using (Graphics g = CreateGraphics())
            {
                SizeF messageSize = g.MeasureString(text, this.messageLabel.Font, this.messageLabel.MaximumSize.Width);
                this.messageLabel.Size = new Size(this.messageLabel.MaximumSize.Width, (int)Math.Ceiling(messageSize.Height));
            }

            int dialogHeight = this.headerPanel.Height + this.messageLabel.Height + this.buttonsPanel.Height + 40; // 40 for padding/margins
            dialogWidth = Math.Max(dialogWidth, this.messageLabel.PreferredWidth + 40); // Ensure dialog is wide enough for message

            // MessageBoxDialog
            this.Text = caption; // Windows title, though FormBorderStyle.None hides it
            this.ClientSize = new Size(dialogWidth, dialogHeight);
            this.Controls.Add(this.messageLabel);
            this.Controls.Add(this.buttonsPanel);
            this.Controls.Add(this.headerPanel);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual; // Centered in OnShown
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;

            // Set AcceptButton and CancelButton after buttons are created
            SetDefaultAndCancelButtons(buttons);


            this.headerPanel.ResumeLayout(false);
            this.buttonsPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private void AddButtons(MessageBoxButtons buttons)
        {
            buttonsPanel.Controls.Clear(); // Clear any existing buttons

            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    okButton = CreateButton("OK", DialogResult.OK);
                    buttonsPanel.Controls.Add(okButton);
                    break;
                case MessageBoxButtons.OKCancel:
                    cancelButton = CreateButton("Cancel", DialogResult.Cancel);
                    okButton = CreateButton("OK", DialogResult.OK);
                    buttonsPanel.Controls.Add(cancelButton);
                    buttonsPanel.Controls.Add(okButton);
                    break;
                case MessageBoxButtons.YesNo:
                    noButton = CreateButton("No", DialogResult.No);
                    yesButton = CreateButton("Yes", DialogResult.Yes);
                    buttonsPanel.Controls.Add(noButton);
                    buttonsPanel.Controls.Add(yesButton);
                    break;
                case MessageBoxButtons.YesNoCancel:
                    cancelButton = CreateButton("Cancel", DialogResult.Cancel);
                    noButton = CreateButton("No", DialogResult.No);
                    yesButton = CreateButton("Yes", DialogResult.Yes);
                    buttonsPanel.Controls.Add(cancelButton);
                    buttonsPanel.Controls.Add(noButton);
                    buttonsPanel.Controls.Add(yesButton);
                    break;
                // AbortRetryIgnore and RetryCancel can be added here
                default: // Default to OK
                    okButton = CreateButton("OK", DialogResult.OK);
                    buttonsPanel.Controls.Add(okButton);
                    break;
            }
        }

        private Button CreateButton(string text, DialogResult dialogResult)
        {
            var button = new Button
            {
                Text = text,
                DialogResult = dialogResult, // Set DialogResult directly
                Size = new Size(100, 30),
                // Add any specific styling like in TemplateConfirmationDialog if needed
            };
            button.Click += (s, e) =>
            {
                this.resultCallback?.Invoke(dialogResult);
                this.Close(); // DialogResult takes care of returning
            };
            return button;
        }

        private void SetDefaultAndCancelButtons(MessageBoxButtons buttons)
        {
            // Default behavior: first button is accept, last is cancel if applicable
            // MessageBoxDefaultButton enum could be used for more fine-grained control later
            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    this.AcceptButton = okButton;
                    break;
                case MessageBoxButtons.OKCancel:
                    this.AcceptButton = okButton;
                    this.CancelButton = cancelButton;
                    break;
                case MessageBoxButtons.YesNo:
                    this.AcceptButton = yesButton;
                    this.CancelButton = noButton; // Or handle Escape key differently if 'No' is not a true cancel
                    break;
                case MessageBoxButtons.YesNoCancel:
                    this.AcceptButton = yesButton;
                    this.CancelButton = cancelButton;
                    break;
                // AbortRetryIgnore and RetryCancel cases
                default:
                    if (okButton != null) this.AcceptButton = okButton;
                    break;
            }
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
                // Center on screen if no owner
                this.CenterToScreen();
            }

            mouseHandler = new DialogHelper.ModalDialogMouseHandler(this, headerPanel, owner);
        }
    }
}