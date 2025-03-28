using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace AppRefiner.Dialogs
{
    public class DebugDialog : Form
    {
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly RichTextBox debugTextBox;
        private readonly Button clearButton;
        private readonly Button exportButton;
        private readonly IntPtr parentHandle;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;
        
        // Static list to store debug messages
        private static readonly List<DebugMessage> debugMessages = new List<DebugMessage>();
        private static readonly int MaxMessages = 1000; // Limit to prevent memory issues
        
        // Keep track of open debug dialog instances
        private static readonly List<WeakReference<DebugDialog>> openDialogs = new List<WeakReference<DebugDialog>>();
        
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// Static method to add a debug message to the log
        /// </summary>
        /// <param name="message">The message text</param>
        /// <param name="type">The message type (Info, Warning, Error)</param>
        public static void Log(string message, DebugMessageType type = DebugMessageType.Info)
        {
            lock (debugMessages)
            {
                debugMessages.Add(new DebugMessage 
                { 
                    Message = message, 
                    Type = type, 
                    Timestamp = DateTime.Now 
                });
                
                // Remove oldest messages if we exceed the limit
                while (debugMessages.Count > MaxMessages)
                {
                    debugMessages.RemoveAt(0);
                }
            }

            // Update any open debug dialogs
            UpdateOpenDialogs();
        }
        
        // Update all open dialog instances
        private static void UpdateOpenDialogs()
        {
            lock (openDialogs)
            {
                // Clean up any closed dialogs
                for (int i = openDialogs.Count - 1; i >= 0; i--)
                {
                    if (!openDialogs[i].TryGetTarget(out var dialog) || dialog.IsDisposed)
                    {
                        openDialogs.RemoveAt(i);
                    }
                    else
                    {
                        // Update the dialog
                        dialog.BeginInvoke(new Action(() => {
                            dialog.AppendLatestMessages();
                        }));
                    }
                }
            }
        }
        
        /// <summary>
        /// Static method to open the debug dialog
        /// </summary>
        /// <param name="parentHwnd">Handle to the parent window</param>
        /// <returns>The created debug dialog instance</returns>
        public static DebugDialog ShowDialog(IntPtr parentHwnd)
        {
            var dialog = new DebugDialog(parentHwnd);
            
            // Register this dialog in the open dialogs list
            lock (openDialogs)
            {
                openDialogs.Add(new WeakReference<DebugDialog>(dialog));
            }
            
            dialog.Show();
            return dialog;
        }
        
        /// <summary>
        /// Constructor for the Debug Dialog
        /// </summary>
        /// <param name="parentHwnd">Handle to the parent window</param>
        public DebugDialog(IntPtr parentHwnd)
        {
            this.parentHandle = parentHwnd;
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.debugTextBox = new RichTextBox();
            this.clearButton = new Button();
            this.exportButton = new Button();
            
            InitializeComponent();
            PositionInParent();
            RefreshDebugMessages();
        }
        
        private void PositionInParent()
        {
            if (parentHandle != IntPtr.Zero)
            {
                RECT parentRect;
                if (GetWindowRect(parentHandle, out parentRect))
                {
                    int parentWidth = parentRect.Right - parentRect.Left;
                    int parentHeight = parentRect.Bottom - parentRect.Top;
                    int parentCenterX = parentRect.Left + (parentWidth / 2);
                    int parentCenterY = parentRect.Top + (parentHeight / 2);

                    // Position the dialog centered in the parent window
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = new Point(
                        parentCenterX - (this.Width / 2),
                        parentCenterY - (this.Height / 2)
                    );
                }
            }
        }
        
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            
            // Position on parent window
            if (parentHandle != IntPtr.Zero)
            {
                PositionInParent();
            }

            // Create the mouse handler if this is a modal dialog
            if (this.Modal && parentHandle != IntPtr.Zero)
            {
                mouseHandler = new DialogHelper.ModalDialogMouseHandler(this, headerPanel, parentHandle);
            }
        }
        
        /// <summary>
        /// Refreshes all debug messages in the text box
        /// </summary>
        public void RefreshDebugMessages()
        {
            debugTextBox.Clear();
            lock (debugMessages)
            {
                foreach (var msg in debugMessages)
                {
                    AppendFormattedMessage(msg);
                }
            }
            
            // Scroll to the end
            ScrollToEnd();
        }
        
        /// <summary>
        /// Appends only the newest messages that aren't in the textbox yet
        /// </summary>
        public void AppendLatestMessages()
        {
            // Get the current count of messages in the textbox (approximated by line count)
            int currentLines = debugTextBox.Lines.Length;
            
            lock (debugMessages)
            {
                // If we have more messages than lines, append the new ones
                if (debugMessages.Count > currentLines)
                {
                    // Append only the new messages
                    for (int i = currentLines; i < debugMessages.Count; i++)
                    {
                        AppendFormattedMessage(debugMessages[i]);
                    }
                    
                    // Scroll to the end
                    ScrollToEnd();
                }
            }
        }
        
        private void ScrollToEnd()
        {
            debugTextBox.SelectionStart = debugTextBox.Text.Length;
            debugTextBox.ScrollToCaret();
        }
        
        private void AppendFormattedMessage(DebugMessage msg)
        {
            string timeStamp = msg.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            
            // Store current selection state
            int currentSelection = debugTextBox.SelectionStart;
            
            // Format timestamp in gray
            debugTextBox.SelectionStart = debugTextBox.TextLength;
            debugTextBox.SelectionLength = 0;
            debugTextBox.SelectionColor = Color.Gray;
            debugTextBox.AppendText($"[{timeStamp}] ");
            
            // Format message type with color
            Color typeColor = Color.Black;
            string typePrefix = "";
            
            switch (msg.Type)
            {
                case DebugMessageType.Info:
                    typeColor = Color.DarkBlue;
                    typePrefix = "INFO";
                    break;
                case DebugMessageType.Warning:
                    typeColor = Color.Orange;
                    typePrefix = "WARN";
                    break;
                case DebugMessageType.Error:
                    typeColor = Color.Red;
                    typePrefix = "ERROR";
                    break;
            }
            
            debugTextBox.SelectionColor = typeColor;
            debugTextBox.AppendText($"[{typePrefix}] ");
            
            // Append the actual message in black
            debugTextBox.SelectionColor = Color.Black;
            debugTextBox.AppendText(msg.Message + Environment.NewLine);
            
            // Restore selection
            debugTextBox.SelectionStart = currentSelection;
            debugTextBox.SelectionLength = 0;
            debugTextBox.SelectionColor = Color.Black;
        }
        
        private void ClearMessages()
        {
            lock (debugMessages)
            {
                debugMessages.Clear();
            }
            RefreshDebugMessages();
        }
        
        private void ExportMessages()
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                saveFileDialog.DefaultExt = "txt";
                saveFileDialog.FileName = $"AppRefiner_Debug_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    StringBuilder sb = new StringBuilder();
                    lock (debugMessages)
                    {
                        foreach (var msg in debugMessages)
                        {
                            string timeStamp = msg.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                            string typeStr = msg.Type.ToString().ToUpper();
                            sb.AppendLine($"[{timeStamp}] [{typeStr}] {msg.Message}");
                        }
                    }
                    
                    try
                    {
                        File.WriteAllText(saveFileDialog.FileName, sb.ToString());
                        MessageBox.Show("Debug log exported successfully.", "Export Complete", 
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to export debug log: {ex.Message}", "Export Failed", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
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
            this.headerLabel.Text = "Debug Console";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;
            
            // Button panel for clear and export
            Panel buttonPanel = new Panel();
            buttonPanel.Dock = DockStyle.Bottom;
            buttonPanel.Height = 40;
            buttonPanel.Padding = new Padding(5);
            
            // Clear button
            this.clearButton.Text = "Clear";
            this.clearButton.Dock = DockStyle.Left;
            this.clearButton.Width = 100;
            this.clearButton.Click += (sender, e) => ClearMessages();
            buttonPanel.Controls.Add(this.clearButton);
            
            // Export button
            this.exportButton.Text = "Export Log";
            this.exportButton.Dock = DockStyle.Right;
            this.exportButton.Width = 100;
            this.exportButton.Click += (sender, e) => ExportMessages();
            buttonPanel.Controls.Add(this.exportButton);
            
            // Debug text box
            this.debugTextBox.Dock = DockStyle.Fill;
            this.debugTextBox.BackColor = Color.White;
            this.debugTextBox.ForeColor = Color.Black;
            this.debugTextBox.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.debugTextBox.ReadOnly = true;
            this.debugTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;

            // DebugDialog
            this.ClientSize = new Size(850, 400);
            this.Controls.Add(this.debugTextBox);
            this.Controls.Add(buttonPanel);
            this.Controls.Add(this.headerPanel);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Name = "DebugDialog";
            this.Text = "Debug Console";
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
    
    /// <summary>
    /// Enum representing the type of debug message
    /// </summary>
    public enum DebugMessageType
    {
        Info,
        Warning,
        Error
    }
    
    /// <summary>
    /// Class representing a debug message
    /// </summary>
    public class DebugMessage
    {
        public string Message { get; set; } = "";
        public DebugMessageType Type { get; set; } = DebugMessageType.Info;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
} 