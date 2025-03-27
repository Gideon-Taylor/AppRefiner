#include "HookManager.h"

// Global variables
HWND g_callbackWindow = NULL;
HHOOK g_wndProcHook = NULL;
HHOOK g_getMsgHook = NULL;
HMODULE g_hModule = NULL;
bool g_enableAutoPairing = false;  // Flag to control auto-pairing feature
HWND g_lastEditorHwnd = NULL;      // Track the last editor HWND that received SCN_CHARADDED

// WndProc hook procedure - for window messages
LRESULT CALLBACK WndProcHook(int nCode, WPARAM wParam, LPARAM lParam) {
    // Always call the next hook if code is less than zero
    if (nCode < 0) {
        return CallNextHookEx(g_wndProcHook, nCode, wParam, lParam);
    }
    
    try {
        CWPSTRUCT* cwp = (CWPSTRUCT*)lParam;
        if (!cwp) {
            return CallNextHookEx(g_wndProcHook, nCode, wParam, lParam);
        }

        // Check for WM_NOTIFY messages
        if (cwp->message == WM_NOTIFY) {
            // Get the NMHDR structure
            NMHDR* nmhdr = (NMHDR*)cwp->lParam;
            if (!nmhdr || !IsWindow(nmhdr->hwndFrom)) {
                return CallNextHookEx(g_wndProcHook, nCode, wParam, lParam);
            }

            // Check if this is a Scintilla control
            char className[256] = { 0 };
            if (GetClassNameA((HWND)nmhdr->hwndFrom, className, sizeof(className)) > 0) {
                // Check if it's a Scintilla control (class name starts with "Scintilla")
                if (strncmp(className, "Scintilla", 9) == 0) {
                    // It's a Scintilla control, so we can treat lParam as SCNotification
                    SCNotification* scn = (SCNotification*)cwp->lParam;

                    if (scn->nmhdr.code == SCN_CHARADDED) {
                        // Check if this is a different editor HWND
                        if (g_lastEditorHwnd != nmhdr->hwndFrom) {
                            // Reset the auto-pair tracker when switching between editors
                            g_autoPairTracker.reset();
                            // Update the last editor HWND
                            g_lastEditorHwnd = (HWND)nmhdr->hwndFrom;
                        }
                        
                        // Handle auto-pairing first
                        HandleAutoPairing((HWND)nmhdr->hwndFrom, scn);
                        // Then handle auto-indentation
                        HandlePeopleCodeAutoIndentation((HWND)nmhdr->hwndFrom, scn);
                        return CallNextHookEx(g_wndProcHook, nCode, wParam, lParam);
                    }

                    if (scn->nmhdr.code == SCN_DWELLSTART) {
                        if (g_callbackWindow && IsWindow(g_callbackWindow)) {
                            SendMessage(g_callbackWindow, WM_SCN_DWELL_START, (WPARAM)scn->position, (LPARAM)0);
                        }
                    } 
                    else if (scn->nmhdr.code == SCN_DWELLEND) {
                        if (g_callbackWindow && IsWindow(g_callbackWindow)) {
                            SendMessage(g_callbackWindow, WM_SCN_DWELL_END, (WPARAM)scn->position, (LPARAM)0);
                        }
                    }
                    else if (scn->nmhdr.code == SCN_SAVEPOINTREACHED) {
                        if (g_callbackWindow && IsWindow(g_callbackWindow)) {
                            SendMessage(g_callbackWindow, WM_SCN_SAVEPOINT_REACHED, (WPARAM)0, (LPARAM)0);
                        }
                    }
                }
            }
        }
    }
    catch (const std::exception& e) {
        char errorMsg[256];
        sprintf_s(errorMsg, "Exception in WndProcHook: %s", e.what());
        OutputDebugStringA(errorMsg);
    }
    catch (...) {
        OutputDebugStringA("Unknown exception in WndProcHook");
    }

    return CallNextHookEx(g_wndProcHook, nCode, wParam, lParam);
}

// GetMessage hook procedure - for thread messages
LRESULT CALLBACK GetMsgHook(int nCode, WPARAM wParam, LPARAM lParam) {
    // Always call the next hook if code is less than zero
    if (nCode < 0) {
        return CallNextHookEx(g_getMsgHook, nCode, wParam, lParam);
    }
    
    try {
        MSG* msg = (MSG*)lParam;
        if (!msg) {
            return CallNextHookEx(g_getMsgHook, nCode, wParam, lParam);
        }

        // Check if this is our custom message for setting the callback window
        if (msg->message == WM_SET_CALLBACK_WINDOW) {
            g_callbackWindow = (HWND)msg->wParam;
            char debugMsg[100];
            sprintf_s(debugMsg, "Set callback window to: %p\n", g_callbackWindow);
            OutputDebugStringA(debugMsg);

            // Mark the message as handled
            msg->message = WM_NULL;
        }
        // Check if this is our message to toggle auto-pairing
        else if (msg->message == WM_TOGGLE_AUTO_PAIRING) {
            g_enableAutoPairing = (msg->wParam != 0);
            char debugMsg[100];
            sprintf_s(debugMsg, "Auto-pairing %s\n", g_enableAutoPairing ? "enabled" : "disabled");
            OutputDebugStringA(debugMsg);

            // Mark the message as handled
            msg->message = WM_NULL;
        }
    }
    catch (const std::exception& e) {
        char errorMsg[256];
        sprintf_s(errorMsg, "Exception in GetMsgHook: %s", e.what());
        OutputDebugStringA(errorMsg);
    }
    catch (...) {
        OutputDebugStringA("Unknown exception in GetMsgHook");
    }

    return CallNextHookEx(g_getMsgHook, nCode, wParam, lParam);
}

// Export functions
extern "C" {
    __declspec(dllexport) HHOOK SetHook(DWORD threadId) {
        // Set both hooks for the specified thread
        g_wndProcHook = SetWindowsHookEx(WH_CALLWNDPROC, WndProcHook, g_hModule, threadId);
        g_getMsgHook = SetWindowsHookEx(WH_GETMESSAGE, GetMsgHook, g_hModule, threadId);

        // Return the WndProc hook as the primary hook handle
        return g_wndProcHook;
    }

    __declspec(dllexport) BOOL Unhook() {
        BOOL result = TRUE;

        if (g_wndProcHook != NULL) {
            result = UnhookWindowsHookEx(g_wndProcHook) && result;
            g_wndProcHook = NULL;
        }

        if (g_getMsgHook != NULL) {
            result = UnhookWindowsHookEx(g_getMsgHook) && result;
            g_getMsgHook = NULL;
        }

        return result;
    }
}
