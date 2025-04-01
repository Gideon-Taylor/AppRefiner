using System;
using System.Drawing;
using System.Windows.Forms;

namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Dialog for viewing text content in a read-only window
    /// </summary>
    public class TextViewDialog : Form
    {
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly RichTextBox contentTextBox;
        private readonly Button closeButton;
        private readonly IntPtr owner;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;

        /// <summary>
        /// Initializes a new instance of the TextViewDialog class
        /// </summary>
        /// <param name="content">The text content to display</param>
        /// <param name="title">The title to display in the header</param>
        /// <param name="owner">The owner window handle</param>
        public TextViewDialog(string content, string title, IntPtr owner = default)
        {
            this.owner = owner;
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.contentTextBox = new RichTextBox();
            this.closeButton = new Button();
            
            InitializeComponent(content, title);
        }

        private void InitializeComponent(string content, string title)
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

            // contentTextBox
            this.contentTextBox.BorderStyle = BorderStyle.FixedSingle;
            this.contentTextBox.Location = new Point(20, 50);
            this.contentTextBox.Size = new Size(660, 400);
            this.contentTextBox.TabIndex = 1;
            this.contentTextBox.ReadOnly = true;
            this.contentTextBox.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.contentTextBox.Text = content;
            this.contentTextBox.WordWrap = false;
            this.contentTextBox.ScrollBars = RichTextBoxScrollBars.Both;

            // closeButton
            this.closeButton.Text = "Close";
            this.closeButton.Size = new Size(100, 30);
            this.closeButton.Location = new Point(580, 470);
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

            // TextViewDialog
            this.Text = title;
            this.ClientSize = new Size(700, 520);
            this.Controls.Add(this.headerPanel);
            this.Controls.Add(this.contentTextBox);
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