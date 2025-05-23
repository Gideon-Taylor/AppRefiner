using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace AppRefiner.Services
{
    /// <summary>
    /// Service to manage WinEvent hooks, specifically for detecting window focus changes.
    /// </summary>
    public class WinEventService : IDisposable
    {
        private nint winEventHook = nint.Zero;
        private NativeMethods.WinEventDelegate? winEventDelegate; // Keep reference to prevent GC
        private SynchronizationContext? syncContext;

        /// <summary>
        /// Event raised when a window, potentially a Scintilla editor, gains focus.
        /// The event is invoked on the synchronization context captured during Start.
        /// </summary>
        public event EventHandler<nint>? WindowFocused;

        public WinEventService()
        {
            // Capture synchronization context for marshalling events back to the UI thread
            syncContext = SynchronizationContext.Current;
        }

        /// <summary>
        /// Starts listening for WinEvents.
        /// </summary>
        public void Start()
        {
            if (winEventHook != nint.Zero) return; // Already hooked

            // Ensure delegate is created only once
            winEventDelegate = new NativeMethods.WinEventDelegate(InternalWinEventProc);

            winEventHook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_OBJECT_FOCUS,       // Event Min
                NativeMethods.EVENT_OBJECT_FOCUS,       // Event Max
                nint.Zero,                            // hmodWinEventProc
                winEventDelegate,                       // lpfnWinEventProc
                0,                                      // idProcess (all)
                0,                                      // idThread (all)
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS
            );

            if (winEventHook == nint.Zero)
            {
                Debug.Log("Failed to set up WinEvent hook.");
                // Consider throwing an exception or logging more severely
            }
            else
            {
                Debug.Log("Successfully set up WinEvent hook for focus events.");
            }
        }

        /// <summary>
        /// Stops listening for WinEvents and cleans up the hook.
        /// </summary>
        public void Stop()
        {
            if (winEventHook != nint.Zero)
            {
                NativeMethods.UnhookWinEvent(winEventHook);
                winEventHook = nint.Zero;
                winEventDelegate = null; // Release reference
                Debug.Log("WinEvent hook stopped.");
            }
        }

        // Internal callback for WinEvents
        private void InternalWinEventProc(nint hWinEventHook, uint eventType, nint hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == NativeMethods.EVENT_OBJECT_FOCUS && hwnd != nint.Zero)
            {
                // Optional: Perform a quick check here if desired (e.g., basic class name check)
                // However, detailed processing should happen in the event handler
                
                // Raise the event, marshalling to the captured context (usually UI thread)
                if (syncContext != null)
                {
                    syncContext.Post(_ => OnWindowFocused(hwnd), null);
                }
                else
                {
                    // If no context, raise directly (might be on a background thread)
                    OnWindowFocused(hwnd);
                }
            }
        }

        protected virtual void OnWindowFocused(nint hwnd)
        {
            WindowFocused?.Invoke(this, hwnd);
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        ~WinEventService()
        {
            Dispose();
        }
    }
} 