using System.Runtime.InteropServices;

namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Helper class for dialog-related functionality
    /// </summary>
    public static class DialogHelper
    {
        // Constants for Windows messages
        public const int WM_NCACTIVATE = 134;
        public const int WA_INACTIVE = 0;
        public const int WA_ACTIVE = 1;
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_LBUTTONDOWN = 0x0201;

        // Mouse hook constants and structures
        private const int WH_MOUSE_LL = 14;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // Delegate for the hook procedure
        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        // Import necessary Win32 functions
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT Point);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// Creates a flicker effect on a panel to draw attention to it
        /// </summary>
        /// <param name="panel">The panel to flicker</param>
        /// <param name="originalColor">The original color of the panel</param>
        /// <param name="attentionColor">The color to flicker to</param>
        /// <param name="cycles">Number of flicker cycles</param>
        /// <param name="interval">Interval between color changes in milliseconds</param>
        /// <returns>The timer that controls the flickering</returns>
        public static System.Windows.Forms.Timer CreateHeaderFlickerEffect(
            Panel panel,
            Color originalColor,
            int cycles = 3,
            int interval = 100)
        {
            var attentionColors = new[] { Color.FromArgb(0, 178, 227), originalColor, Color.FromArgb(255, 158, 24) };

            int flickerCount = 0;
            int maxFlickerCount = cycles * 2; // Each cycle is two color changes

            System.Windows.Forms.Timer flickerTimer = new();
            flickerTimer.Interval = interval;
            flickerTimer.Tick += (s, e) =>
            {
                var attentionColor = attentionColors[flickerCount % attentionColors.Length];
                panel.BackColor = attentionColor;

                flickerCount++;

                // Stop after max flicker count
                if (flickerCount >= maxFlickerCount)
                {
                    flickerTimer.Stop();
                    panel.BackColor = originalColor;

                    // Dispose the timer after it's done
                    flickerTimer.Dispose();
                }
            };

            // Start the timer
            flickerTimer.Start();

            return flickerTimer;
        }

        /// <summary>
        /// Class to handle mouse click detection for modal dialogs
        /// </summary>
        public class ModalDialogMouseHandler : IDisposable
        {
            private readonly Form _dialog;
            private readonly Panel _headerPanel;
            private readonly IntPtr _ownerHandle;
            private readonly Color _originalHeaderColor;
            private IntPtr _hookId = IntPtr.Zero;
            private HookProc? _hookProc;
            private System.Windows.Forms.Timer? _flickerTimer;
            private bool _isFlickering = false;

            public ModalDialogMouseHandler(Form dialog, Panel headerPanel, IntPtr ownerHandle)
            {
                _dialog = dialog;
                _headerPanel = headerPanel;
                _ownerHandle = ownerHandle;
                _originalHeaderColor = headerPanel.BackColor;

                // Set up the hook
                _hookProc = HookCallback;
                _hookId = SetHook(_hookProc);

                // Make sure to unhook when the dialog closes
                _dialog.FormClosed += (s, e) => Dispose();
            }

            private IntPtr SetHook(HookProc proc)
            {
                using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
                using var curModule = curProcess.MainModule;
                string moduleName = curModule?.ModuleName ?? string.Empty;
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(moduleName), 0);
            }

            private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
            {
                if (nCode >= 0 && wParam == WM_LBUTTONDOWN && !_isFlickering)
                {
                    // Get the mouse position
                    var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    var point = new POINT { x = hookStruct.pt.x, y = hookStruct.pt.y };

                    // Get the window at the mouse position
                    IntPtr windowAtPoint = WindowFromPoint(point);

                    // Check if the click is on the owner window but not on the dialog
                    if (windowAtPoint == _ownerHandle && _dialog.Modal)
                    {
                        // Get the dialog's position
                        if (GetWindowRect(_dialog.Handle, out RECT dialogRect))
                        {
                            // Check if the click is outside the dialog
                            if (point.x < dialogRect.Left || point.x > dialogRect.Right ||
                                point.y < dialogRect.Top || point.y > dialogRect.Bottom)
                            {
                                // Flicker the header
                                FlickerHeaderBackground();

                                // Return a non-zero value to prevent the click from being processed
                                // This prevents the click from being sent to the owner window
                                return 1;
                            }
                        }
                    }
                }

                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            private void FlickerHeaderBackground()
            {
                if (_isFlickering)
                    return;

                _isFlickering = true;

                // Use the helper to create the flicker effect
                _flickerTimer = CreateHeaderFlickerEffect(_headerPanel, _originalHeaderColor);

                // Reset the flag when the flickering is done
                _flickerTimer.Tick += (s, e) =>
                {
                    if (!_flickerTimer.Enabled)
                    {
                        _isFlickering = false;
                    }
                };
            }

            public void Dispose()
            {
                if (_hookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookId);
                    _hookId = IntPtr.Zero;
                }

                if (_flickerTimer != null)
                {
                    _flickerTimer.Stop();
                    _flickerTimer.Dispose();
                    _flickerTimer = null;
                }
            }
        }
    }
}
