using System.Runtime.InteropServices;

namespace AppRefiner
{
    public class CommandProgressDialog : Form
    {
        private Panel headerPanel;
        private Label headerLabel;
        private ProgressBar progressBar;
        private IntPtr parentHandle;

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

        public CommandProgressDialog(IntPtr parentHwnd)
        {
            this.parentHandle = parentHwnd;
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.progressBar = new ProgressBar();
            InitializeComponent();
            PositionInParent();
        }

        public void UpdateHeader(string text)
        {
            this.Invoke(() =>
            {
                headerLabel.Text = text;
            });
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
