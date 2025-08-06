using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AppRefiner.Dialogs
{
    public class LintProjectProgressDialog : Form
    {
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly ProgressBar progressBar;
        private readonly Label statusLabel;
        private readonly Button cancelButton;
        private readonly IntPtr parentHandle;
        private readonly LinterManager linterManager;
        private readonly ScintillaEditor editorContext;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;
        private BackgroundWorker? backgroundWorker;
        private bool isCancelled = false;

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

        public LintProjectProgressDialog(LinterManager linterManager, ScintillaEditor editorContext, IntPtr parentHwnd)
        {
            this.linterManager = linterManager ?? throw new ArgumentNullException(nameof(linterManager));
            this.editorContext = editorContext ?? throw new ArgumentNullException(nameof(editorContext));
            this.parentHandle = parentHwnd;
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.progressBar = new ProgressBar();
            this.statusLabel = new Label();
            this.cancelButton = new Button();
            InitializeComponent();
            PositionInParent();
        }

        public void UpdateHeader(string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() => headerLabel.Text = text);
            }
            else
            {
                headerLabel.Text = text;
            }
        }

        public void UpdateProgress(string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() => statusLabel.Text = text);
            }
            else
            {
                statusLabel.Text = text;
            }
        }

        public void UpdateProgressBar(int current, int total)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() =>
                {
                    progressBar.Style = ProgressBarStyle.Blocks;
                    progressBar.Maximum = total;
                    progressBar.Value = Math.Min(current, total);
                });
            }
            else
            {
                progressBar.Style = ProgressBarStyle.Blocks;
                progressBar.Maximum = total;
                progressBar.Value = Math.Min(current, total);
            }
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
            
            // Center on owner window
            if (parentHandle != IntPtr.Zero)
            {
                WindowHelper.CenterFormOnWindow(this, parentHandle);
            }

            // Create the mouse handler if this is a modal dialog
            if (this.Modal && parentHandle != IntPtr.Zero)
            {
                mouseHandler = new DialogHelper.ModalDialogMouseHandler(this, headerPanel, parentHandle);
            }

            // Start the linting process
            StartLintProject();
        }

        private void StartLintProject()
        {
            backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerReportsProgress = true;
            backgroundWorker.WorkerSupportsCancellation = true;
            backgroundWorker.DoWork += BackgroundWorker_DoWork;
            backgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
            backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            backgroundWorker.RunWorkerAsync();
        }

        private void BackgroundWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            try
            {
                ExecuteLintProject();
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        private void BackgroundWorker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            if (e.UserState is string message)
            {
                if (message.StartsWith("HEADER:"))
                {
                    UpdateHeader(message.Substring(7));
                }
                else if (message.StartsWith("STATUS:"))
                {
                    UpdateProgress(message.Substring(7));
                }
                else if (message.StartsWith("PROGRESS:"))
                {
                    var parts = message.Substring(9).Split('|');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int current) && int.TryParse(parts[1], out int total))
                    {
                        UpdateProgressBar(current, total);
                    }
                }
            }
        }

        private void BackgroundWorker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is Exception ex)
            {
                // Show error message
                ShowMessageBox("Error during project linting: " + ex.Message, "Linting Error", MessageBoxButtons.OK);
            }
            else if (!isCancelled)
            {
                // Success - dialog will close automatically
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                this.DialogResult = DialogResult.Cancel;
            }
            
            this.Close();
        }

        private void ExecuteLintProject()
        {
            // Create callbacks for progress updates
            Action<string> updateHeaderCallback = (header) => 
            {
                backgroundWorker?.ReportProgress(0, $"HEADER:{header}");
            };

            Action<string> updateStatusCallback = (status) => 
            {
                backgroundWorker?.ReportProgress(0, $"STATUS:{status}");
            };

            Action<int, int> updateProgressCallback = (current, total) => 
            {
                backgroundWorker?.ReportProgress(0, $"PROGRESS:{current}|{total}");
            };

            Func<bool> shouldCancelCallback = () => 
            {
                return backgroundWorker?.CancellationPending == true || isCancelled;
            };

            // Delegate to LinterManager with callbacks
            linterManager.LintProject(
                editorContext,
                updateHeaderCallback,
                updateStatusCallback,
                updateProgressCallback,
                shouldCancelCallback
            );
        }


        private DialogResult ShowMessageBox(string message, string title, MessageBoxButtons buttons)
        {
            DialogResult result = DialogResult.OK;
            this.Invoke(() =>
            {
                var handleWrapper = new WindowWrapper(parentHandle);
                var dialog = new MessageBoxDialog(message, title, buttons, parentHandle);
                result = dialog.ShowDialog(handleWrapper);
            });
            return result;
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            if (backgroundWorker != null && backgroundWorker.IsBusy)
            {
                isCancelled = true;
                backgroundWorker.CancelAsync();
                cancelButton.Enabled = false;
                cancelButton.Text = "Cancelling...";
                UpdateHeader("Cancelling...");
                UpdateProgress("Please wait while cancellation completes...");
            }
            else
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
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
            this.headerLabel.Text = "Linting Project...";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // progressBar
            this.progressBar.Dock = DockStyle.Top;
            this.progressBar.Style = ProgressBarStyle.Marquee;
            this.progressBar.MarqueeAnimationSpeed = 30;
            this.progressBar.Location = new Point(0, 30);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new Size(400, 20);
            this.progressBar.Height = 20;

            // statusLabel
            this.statusLabel.Text = "Initializing...";
            this.statusLabel.ForeColor = Color.Black;
            this.statusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.statusLabel.Dock = DockStyle.Top;
            this.statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.statusLabel.Height = 25;
            this.statusLabel.Padding = new Padding(5);

            // cancelButton
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Size = new Size(75, 25);
            this.cancelButton.Location = new Point(162, 80);
            this.cancelButton.Anchor = AnchorStyles.Bottom;
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += CancelButton_Click;

            // LintProjectProgressDialog
            this.ClientSize = new Size(400, 115);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.statusLabel);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.headerPanel);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Name = "LintProjectProgressDialog";
            this.Text = "Linting Project";
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            
            // Clean up background worker
            if (backgroundWorker != null)
            {
                backgroundWorker.CancelAsync();
                backgroundWorker.Dispose();
                backgroundWorker = null;
            }
            
            // Dispose the mouse handler
            mouseHandler?.Dispose();
            mouseHandler = null;
        }
    }
}