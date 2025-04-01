using System;
using System.Drawing;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Dialog for viewing Git diff content with syntax highlighting for changes
    /// </summary>
    public class DiffViewDialog : Form
    {
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly RichTextBox diffTextBox;
        private readonly Button closeButton;
        private readonly IntPtr owner;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;

        // Colors for diff highlighting
        private static readonly Color AddedColor = Color.FromArgb(200, 255, 200); // Light green
        private static readonly Color RemovedColor = Color.FromArgb(255, 200, 200); // Light red
        private static readonly Color HeaderColor = Color.FromArgb(220, 220, 255); // Light blue
        private static readonly Color HunkHeaderColor = Color.FromArgb(240, 240, 240); // Light gray

        /// <summary>
        /// Initializes a new instance of the DiffViewDialog class
        /// </summary>
        /// <param name="diffContent">The diff content to display</param>
        /// <param name="title">The title to display in the header</param>
        /// <param name="owner">The owner window handle</param>
        public DiffViewDialog(string diffContent, string title, IntPtr owner = default)
        {
            this.owner = owner;
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.diffTextBox = new RichTextBox();
            this.closeButton = new Button();
            
            InitializeComponent(title);
            FormatDiffContent(diffContent);
        }

        private void InitializeComponent(string title)
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
            this.headerLabel.Text = title;
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TabIndex = 0;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // diffTextBox
            this.diffTextBox.BorderStyle = BorderStyle.FixedSingle;
            this.diffTextBox.Location = new Point(20, 50);
            this.diffTextBox.Size = new Size(760, 450);
            this.diffTextBox.TabIndex = 1;
            this.diffTextBox.ReadOnly = true;
            this.diffTextBox.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.diffTextBox.WordWrap = false;
            this.diffTextBox.ScrollBars = RichTextBoxScrollBars.Both;
            this.diffTextBox.BackColor = Color.White;

            // closeButton
            this.closeButton.Text = "Close";
            this.closeButton.Size = new Size(100, 30);
            this.closeButton.Location = new Point(680, 520);
            this.closeButton.TabIndex = 2;
            this.closeButton.BackColor = Color.FromArgb(100, 100, 100);
            this.closeButton.ForeColor = Color.White;
            this.closeButton.FlatStyle = FlatStyle.Flat;
            this.closeButton.FlatAppearance.BorderSize = 0;
            this.closeButton.Click += (s, e) => 
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            // DiffViewDialog
            this.Text = title;
            this.ClientSize = new Size(800, 570);
            this.Controls.Add(this.headerPanel);
            this.Controls.Add(this.diffTextBox);
            this.Controls.Add(this.closeButton);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(240, 240, 245);
            this.Padding = new Padding(1);

            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        /// <summary>
        /// Formats the diff content with colorized highlighting for additions, deletions, and headers
        /// </summary>
        /// <param name="diffContent">The raw diff content</param>
        private void FormatDiffContent(string diffContent)
        {
            if (string.IsNullOrEmpty(diffContent))
            {
                diffTextBox.Text = "No changes detected.";
                return;
            }

            diffTextBox.Clear();
            diffTextBox.SuspendLayout();

            // Split the diff into lines for processing
            string[] lines = diffContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                
                // Detect the type of line and apply appropriate formatting
                if (line.StartsWith("diff --git") || line.StartsWith("index ") || 
                    line.StartsWith("--- ") || line.StartsWith("+++ "))
                {
                    // Diff header - blue background
                    AppendFormattedText(line, Color.Black, HeaderColor);
                }
                else if (line.StartsWith("@@") && line.Contains("@@"))
                {
                    // Hunk header - gray background
                    AppendFormattedText(line, Color.FromArgb(70, 70, 100), HunkHeaderColor);
                }
                else if (line.StartsWith("+"))
                {
                    // Added line - green background
                    AppendFormattedText(line, Color.Black, AddedColor);
                }
                else if (line.StartsWith("-"))
                {
                    // Removed line - red background
                    AppendFormattedText(line, Color.Black, RemovedColor);
                }
                else
                {
                    // Context line - default background
                    AppendFormattedText(line, Color.Black, Color.White);
                }
            }

            diffTextBox.SelectionStart = 0;
            diffTextBox.SelectionLength = 0;
            diffTextBox.ResumeLayout();
        }

        /// <summary>
        /// Appends text to the RichTextBox with the specified text and background colors
        /// </summary>
        /// <param name="text">The text to append</param>
        /// <param name="textColor">The color of the text</param>
        /// <param name="backgroundColor">The background color for the text</param>
        private void AppendFormattedText(string text, Color textColor, Color backgroundColor)
        {
            int start = diffTextBox.TextLength;
            diffTextBox.AppendText(text + Environment.NewLine);
            int end = diffTextBox.TextLength;

            // Set text and background colors
            diffTextBox.Select(start, end - start);
            diffTextBox.SelectionColor = textColor;
            diffTextBox.SelectionBackColor = backgroundColor;
            diffTextBox.SelectionLength = 0;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            
            // Center on owner window
            if (owner != IntPtr.Zero)
            {
                WindowHelper.CenterFormOnWindow(this, owner);
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
            using (var pen = new Pen(Color.FromArgb(100, 100, 120)))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
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