using System.Diagnostics;

namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Dialog for displaying type error reports and allowing users to submit them to GitHub
    /// or copy them to the clipboard.
    /// </summary>
    public class TypeErrorReportDialog : Form
    {
        private readonly string _report;
        private readonly IntPtr _ownerHandle;

        // UI Components
        private Panel headerPanel;
        private Label titleLabel;
        private Button closeButton;
        private Panel contentPanel;
        private TextBox reportTextBox;
        private Panel buttonPanel;
        private Button openGitHubButton;
        private Button copyToClipboardButton;
        private Button closeDialogButton;

        // Modal dialog mouse handler
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;

        // Variables for drag functionality
        private bool isDragging = false;
        private Point dragStartPoint;

        public TypeErrorReportDialog(string report, IntPtr ownerHandle)
        {
            _report = report ?? throw new ArgumentNullException(nameof(report));
            _ownerHandle = ownerHandle;

            InitializeUI();
            SetupEventHandlers();

            // Apply AppRefiner dialog styling
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.FromArgb(240, 240, 245);
            this.Font = new Font("Segoe UI", 9F);
            this.Size = new Size(700, 500);

            // Center on owner window
            if (ownerHandle != IntPtr.Zero)
            {
                WindowHelper.CenterFormOnWindow(this, ownerHandle);
            }

            // Setup shown event for modal behavior
            this.Shown += TypeErrorReportDialog_Shown;
        }

        private void TypeErrorReportDialog_Shown(object? sender, EventArgs e)
        {
            // Create the mouse handler if this is a modal dialog
            if (this.Modal && _ownerHandle != IntPtr.Zero)
            {
                mouseHandler = new DialogHelper.ModalDialogMouseHandler(this, headerPanel, _ownerHandle);
            }
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Draw a border around the form
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle,
                Color.FromArgb(100, 100, 120), // Border color
                1, ButtonBorderStyle.Solid,    // Left
                Color.FromArgb(100, 100, 120), // Border color
                1, ButtonBorderStyle.Solid,    // Top
                Color.FromArgb(100, 100, 120), // Border color
                1, ButtonBorderStyle.Solid,    // Right
                Color.FromArgb(100, 100, 120), // Border color
                1, ButtonBorderStyle.Solid);   // Bottom
        }

        /// <summary>
        /// Initializes the UI components
        /// </summary>
        private void InitializeUI()
        {
            this.SuspendLayout();

            // Create header panel (dark)
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(45, 45, 48),
                Padding = new Padding(10, 0, 10, 0)
            };

            // Title label
            titleLabel = new Label
            {
                Text = "Type Error Report",
                Dock = DockStyle.Left,
                AutoSize = false,
                Width = 200,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Close button
            closeButton = new Button
            {
                Text = "✕",
                Dock = DockStyle.Right,
                Width = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F),
                Cursor = Cursors.Hand
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 62, 66);

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(closeButton);

            // Create content panel
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15)
            };

            // Report text box
            reportTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = false,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = _report,
                AcceptsReturn = true,
                AcceptsTab = true
            };

            contentPanel.Controls.Add(reportTextBox);

            // Create button panel
            buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                Padding = new Padding(15, 10, 15, 15)
            };

            // Open GitHub button
            openGitHubButton = new Button
            {
                Text = "Open GitHub Issue",
                Width = 150,
                Height = 35,
                Location = new Point(15, 10),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            openGitHubButton.FlatAppearance.BorderSize = 0;

            // Copy to clipboard button
            copyToClipboardButton = new Button
            {
                Text = "Copy to Clipboard",
                Width = 150,
                Height = 35,
                Location = new Point(175, 10),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            copyToClipboardButton.FlatAppearance.BorderSize = 0;

            // Close dialog button
            closeDialogButton = new Button
            {
                Text = "Close",
                Width = 100,
                Height = 35,
                Location = new Point(335, 10),
                BackColor = Color.FromArgb(90, 90, 90),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            closeDialogButton.FlatAppearance.BorderSize = 0;

            buttonPanel.Controls.Add(openGitHubButton);
            buttonPanel.Controls.Add(copyToClipboardButton);
            buttonPanel.Controls.Add(closeDialogButton);

            // Add panels to form
            this.Controls.Add(contentPanel);
            this.Controls.Add(buttonPanel);
            this.Controls.Add(headerPanel);

            this.ResumeLayout(false);
        }

        /// <summary>
        /// Sets up event handlers
        /// </summary>
        private void SetupEventHandlers()
        {
            // Close button
            closeButton.Click += (s, e) => this.Close();
            closeDialogButton.Click += (s, e) => this.Close();

            // Open GitHub button
            openGitHubButton.Click += OpenGitHub_Click;

            // Copy to clipboard button
            copyToClipboardButton.Click += CopyToClipboard_Click;

            // Make header draggable
            headerPanel.MouseDown += Header_MouseDown;
            headerPanel.MouseMove += Header_MouseMove;
            headerPanel.MouseUp += Header_MouseUp;
            titleLabel.MouseDown += Header_MouseDown;
            titleLabel.MouseMove += Header_MouseMove;
            titleLabel.MouseUp += Header_MouseUp;

            // Escape key closes dialog
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.Close();
                }
            };
        }

        /// <summary>
        /// Opens GitHub issues page with pre-filled report
        /// </summary>
        private void OpenGitHub_Click(object? sender, EventArgs e)
        {
            try
            {
                // Use the edited text from the textbox
                string reportText = reportTextBox.Text;
                string title = Uri.EscapeDataString("Type Checker Issue Report");
                string body = Uri.EscapeDataString(reportText);

                // GitHub has URL length limits, so truncate if necessary
                if (body.Length > 7000)
                {
                    body = Uri.EscapeDataString(reportText.Substring(0, 5000) + "\n\n[Report truncated due to length]");
                }

                string url = $"https://github.com/Gideon-Taylor/AppRefiner/issues/new?title={title}&body={body}";

                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                this.Close();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Failed to open GitHub issue");
                MessageBox.Show($"Failed to open GitHub: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Copies report to clipboard
        /// </summary>
        private void CopyToClipboard_Click(object? sender, EventArgs e)
        {
            try
            {
                // Use the edited text from the textbox
                Clipboard.SetText(reportTextBox.Text);
                MessageBox.Show("Report copied to clipboard!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Failed to copy to clipboard");
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Handles mouse down for dragging
        /// </summary>
        private void Header_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                dragStartPoint = e.Location;
            }
        }

        /// <summary>
        /// Handles mouse move for dragging
        /// </summary>
        private void Header_MouseMove(object? sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point newLocation = this.Location;
                newLocation.X += e.X - dragStartPoint.X;
                newLocation.Y += e.Y - dragStartPoint.Y;
                this.Location = newLocation;
            }
        }

        /// <summary>
        /// Handles mouse up for dragging
        /// </summary>
        private void Header_MouseUp(object? sender, MouseEventArgs e)
        {
            isDragging = false;
        }
    }
}
