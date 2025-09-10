using System.Diagnostics;
using System.Windows.Forms;

namespace AppRefiner.Services
{
    /// <summary>
    /// Service for automatically centering modal dialog windows owned by Application Designer processes.
    /// </summary>
    public class DialogCenteringService
    {
        private readonly SettingsService settingsService;

        public DialogCenteringService(SettingsService settingsService)
        {
            this.settingsService = settingsService;
        }

        /// <summary>
        /// Attempts to center a dialog window if it meets the criteria for auto-centering.
        /// </summary>
        /// <param name="hwnd">The window handle to potentially center</param>
        /// <param name="processId">The process ID that owns the window</param>
        public void TryCenterDialog(IntPtr hwnd, uint processId)
        {
            try
            {
                // Validate the window handle
                if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
                {
                    return;
                }

                // Check if this window is a modal dialog
                if (!IsModalDialog(hwnd))
                {
                    return;
                }

                // Get the owner window (should be the main Application Designer window)
                IntPtr ownerHwnd = NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER);
                if (ownerHwnd == IntPtr.Zero)
                {
                    return;
                }

                // Center the dialog over its owner
                CenterDialogOverOwner(hwnd, ownerHwnd);

                Debug.Log($"Centered dialog window 0x{hwnd.ToInt64():X} over owner 0x{ownerHwnd.ToInt64():X} for process {processId}");
            }
            catch (Exception ex)
            {
                Debug.Log($"Error in TryCenterDialog: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines if a window has an owner (indicating it's likely a modal dialog).
        /// </summary>
        /// <param name="hwnd">The window handle to check</param>
        /// <returns>True if the window has an owner window</returns>
        private bool IsModalDialog(IntPtr hwnd)
        {
            try
            {
                // Get the owner window - dialogs should have an owner
                IntPtr ownerHwnd = NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER);
                return ownerHwnd != IntPtr.Zero;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error in IsModalDialog: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Centers a dialog window over its owner window.
        /// </summary>
        /// <param name="dialogHwnd">The dialog window handle</param>
        /// <param name="ownerHwnd">The owner window handle</param>
        private void CenterDialogOverOwner(IntPtr dialogHwnd, IntPtr ownerHwnd)
        {
            try
            {
                // Get owner window rectangle
                if (!NativeMethods.GetWindowRect(ownerHwnd, out NativeMethods.RECT ownerRect))
                {
                    return;
                }

                // Get dialog window rectangle
                if (!NativeMethods.GetWindowRect(dialogHwnd, out NativeMethods.RECT dialogRect))
                {
                    return;
                }

                // Calculate owner window dimensions and center point
                int ownerWidth = ownerRect.Right - ownerRect.Left;
                int ownerHeight = ownerRect.Bottom - ownerRect.Top;
                int ownerCenterX = ownerRect.Left + (ownerWidth / 2);
                int ownerCenterY = ownerRect.Top + (ownerHeight / 2);

                // Calculate dialog dimensions
                int dialogWidth = dialogRect.Right - dialogRect.Left;
                int dialogHeight = dialogRect.Bottom - dialogRect.Top;

                // Calculate new position to center dialog over owner
                int newX = ownerCenterX - (dialogWidth / 2);
                int newY = ownerCenterY - (dialogHeight / 2);

                // Ensure dialog stays within screen bounds
                var screen = System.Windows.Forms.Screen.FromHandle(ownerHwnd);
                if (screen != null)
                {
                    newX = Math.Max(screen.WorkingArea.Left, Math.Min(newX, screen.WorkingArea.Right - dialogWidth));
                    newY = Math.Max(screen.WorkingArea.Top, Math.Min(newY, screen.WorkingArea.Bottom - dialogHeight));
                }

                // Position the dialog window
                NativeMethods.SetWindowPos(
                    dialogHwnd,
                    NativeMethods.HWND_TOP,
                    newX,
                    newY,
                    0, 0, // Don't change size
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE
                );
            }
            catch (Exception ex)
            {
                Debug.Log($"Error in CenterDialogOverOwner: {ex.Message}");
            }
        }
    }
}