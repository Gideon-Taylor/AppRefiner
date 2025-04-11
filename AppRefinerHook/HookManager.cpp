#include "HookManager.h"

// Global variables
HHOOK g_getMsgHook = NULL;
HMODULE g_hModule = NULL;
bool g_enableAutoPairing = false;  // Flag to control auto-pairing feature
HWND g_lastEditorHwnd = NULL;      // Track the last editor HWND that received SCN_CHARADDED
HMODULE g_dllSelfReference = NULL; // Self-reference to prevent DLL unloading

// Subclass ID for our window subclassing
const UINT_PTR SUBCLASS_ID = 1001;

// Function to handle Scintilla notifications
void HandleScintillaNotification(HWND hwnd, SCNotification* scn, HWND callbackWindow) {
    if (!scn || !hwnd || !IsWindow(hwnd)) return;
    
    try {
        // Handle typing events for EditorManager
        if (scn->nmhdr.code == SCN_CHARADDED || 
            (scn->nmhdr.code == SCN_MODIFIED && 
             ((scn->modificationType & SC_MOD_INSERTTEXT) || 
              (scn->modificationType & SC_MOD_DELETETEXT)))) {
            // Notify EditorManager about text change event (typing, deletion, cut)
            EditorManager::HandleTextChangeEvent(hwnd, callbackWindow);
        }
        
        if (scn->nmhdr.code == SCN_CHARADDED) {
            // Check if this is a different editor HWND
            if (g_lastEditorHwnd != hwnd) {
                // Reset the auto-pair tracker when switching between editors
                g_autoPairTracker.reset();
                // Update the last editor HWND
                g_lastEditorHwnd = hwnd;
            }
            
            // Check for colon character to trigger app package auto-suggest
            if (scn->ch == ':' && callbackWindow && IsWindow(callbackWindow)) {
                // Get the current position
                int currentPos = SendMessage(hwnd, SCI_GETCURRENTPOS, 0, 0);
                
                // Debug: Get autocompletion settings
                bool autoHide = SendMessage(hwnd, SCI_AUTOCGETAUTOHIDE, 0, 0) != 0;
                int separator = SendMessage(hwnd, SCI_AUTOCGETSEPARATOR, 0, 0);
                char debugMsg[256];
                sprintf_s(debugMsg, "Autocompletion settings - AutoHide: %s, Separator: '%c' (%d)\n", 
                          autoHide ? "true" : "false", (char)separator, separator);
                OutputDebugStringA(debugMsg);
                
                // Send the app package suggest message with current position as wParam
                SendMessage(callbackWindow, WM_AR_APP_PACKAGE_SUGGEST, (WPARAM)currentPos, 0);
            }
            
            // Verify the window is still valid before proceeding
            if (IsWindow(hwnd)) {
                // Handle auto-pairing first
                HandleAutoPairing(hwnd, scn);
                
                // Verify window is still valid
                if (IsWindow(hwnd)) {
                    // Then handle auto-indentation
                    HandlePeopleCodeAutoIndentation(hwnd, scn);
                }
            }

            // Check for opening parenthesis to handle auto-pairing and create shorthand
            if (scn->ch == '(' && callbackWindow && IsWindow(callbackWindow)) {
                int currentPos = SendMessage(hwnd, SCI_GETCURRENTPOS, 0, 0);
                
                // First check if this was preceded by "create" to handle create shorthand
                const char* createKeyword = "create";
                int keywordLength = 6; // "create" length
                
                // Check if we have enough characters before the current position
                if (currentPos >= keywordLength) {
                    // Get the characters before the current position
                    char buffer[7] = { 0 }; // "create" + null terminator
                    Sci_TextRange tr;
                    tr.chrg.cpMin = currentPos - keywordLength - 1;
                    tr.chrg.cpMax = currentPos - 1; // -1 because current position is after the '('
                    tr.lpstrText = buffer;
                    
                    SendMessage(hwnd, SCI_GETTEXTRANGE, 0, (LPARAM)&tr);
                    
                    // Convert to lowercase for case-insensitive comparison
                    for (int i = 0; i < keywordLength - 1; i++) {
                        buffer[i] = tolower(buffer[i]);
                    }
                    
                    // Check if the text matches "create"
                    if (strcmp(buffer, createKeyword) == 0) {
                        char debugMsg[100];
                        sprintf_s(debugMsg, "Detected 'create(' pattern at position %d\n", currentPos);
                        OutputDebugStringA(debugMsg);
                        
                        // Send the create shorthand message with auto-pairing status as wParam
                        // and current position as lParam
                        SendMessage(callbackWindow, WM_AR_CREATE_SHORTHAND, (WPARAM)g_enableAutoPairing, (LPARAM)currentPos);
                    }
                }
            }

        }
        else if (scn->nmhdr.code == SCN_DWELLSTART) {
            if (callbackWindow && IsWindow(callbackWindow)) {
                // Get the Scintilla editor handle
                HWND scintillaHwnd = scn->nmhdr.hwndFrom;
                
                // Get the line number from the position
                int line = -1;
                if (scintillaHwnd && IsWindow(scintillaHwnd)) {
                    // Use Scintilla message to get line from position
                    line = SendMessage(scintillaHwnd, SCI_LINEFROMPOSITION, scn->position, 0);
                    // Line is 0-based from Scintilla, convert to 1-based for our API
                    line++;
                }
                
                // Send message with position as wParam and line number as lParam
                SendMessage(callbackWindow, WM_SCN_DWELL_START, (WPARAM)scn->position, (LPARAM)line);
            }
        } 
        else if (scn->nmhdr.code == SCN_DWELLEND) {
            if (callbackWindow && IsWindow(callbackWindow)) {
                SendMessage(callbackWindow, WM_SCN_DWELL_END, (WPARAM)scn->position, (LPARAM)0);
            }
        }
        else if (scn->nmhdr.code == SCN_SAVEPOINTREACHED) {
            if (callbackWindow && IsWindow(callbackWindow)) {
                SendMessage(callbackWindow, WM_SCN_SAVEPOINT_REACHED, (WPARAM)0, (LPARAM)0);
            }
        }
        else if (scn->nmhdr.code == SCN_USERLISTSELECTION) {
            if (callbackWindow && IsWindow(callbackWindow)) {
                // Output debug info
                char debugMsg[256];
				sprintf_s(debugMsg, "User list selection: %s\n", scn->text ? scn->text : "NULL");
				OutputDebugStringA(debugMsg);
				
                // Check if this is an app package completion (listType == 1)
                if (scn->listType == 1 && hwnd && IsWindow(hwnd)) {
                    // Get the current position
                    int currentPos = SendMessage(hwnd, SCI_GETCURRENTPOS, 0, 0);
                    
                    // Get the position where the autocompletion list was opened
                    int startPos = SendMessage(hwnd, SCI_AUTOCPOSSTART, 0, 0);
                    
                    // If we found position to select (should be valid if we're handling a selection)
                    if (startPos >= 0 && startPos < currentPos) {
                        // Select the text that will be replaced
                        SendMessage(hwnd, SCI_SETSEL, startPos, currentPos);
                        
                        char debugMsg[100];
                        sprintf_s(debugMsg, "App package completion: selecting from pos %d to %d\n", startPos, currentPos);
                        OutputDebugStringA(debugMsg);
                    }
                }
                
                // Forward the user list selection to the callback window
                SendMessage(callbackWindow, WM_SCN_USERLIST_SELECTION, (WPARAM)scn->listType, (LPARAM)scn->text);
            }
        }
    }
    catch (const std::exception& e) {
        char errorMsg[256];
        sprintf_s(errorMsg, "Exception in HandleScintillaNotification: %s", e.what());
        OutputDebugStringA(errorMsg);
    }
    catch (...) {
        OutputDebugStringA("Unknown exception in HandleScintillaNotification");
    }
}

// Subclass procedure for handling window messages
LRESULT CALLBACK SubclassProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData) {
    try {
        // Handle WM_NCDESTROY message to remove subclassing and release references
        if (uMsg == WM_NCDESTROY) {
            OutputDebugStringA("Window being destroyed, removing subclass");
            RemoveWindowSubclass(hWnd, SubclassProc, SUBCLASS_ID);            
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
        
        // Get the callback window from dwRefData
        HWND callbackWindow = (HWND)dwRefData;
        
        // Check for WM_NOTIFY messages
        if (uMsg == WM_NOTIFY) {
            // Get the NMHDR structure
            NMHDR* nmhdr = (NMHDR*)lParam;
            if (!nmhdr) {
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
            }
            
            // Validate hwndFrom
            if (!nmhdr->hwndFrom || !IsWindow(nmhdr->hwndFrom)) {
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
            }

            // Check if this is a Scintilla control
            char className[256] = { 0 };
            if (GetClassNameA(nmhdr->hwndFrom, className, sizeof(className)) > 0) {
                // Check if it's a Scintilla control (class name starts with "Scintilla")
                if (strncmp(className, "Scintilla", 9) == 0) {
                    // It's a Scintilla control, so we can treat lParam as SCNotification
                    SCNotification* scn = (SCNotification*)lParam;
                    
                    // Process the Scintilla notification with the callback window
                    HandleScintillaNotification(nmhdr->hwndFrom, scn, callbackWindow);
                }
            }
        }
    }
    catch (const std::exception& e) {
        char errorMsg[256];
        sprintf_s(errorMsg, "Exception in SubclassProc: %s", e.what());
        OutputDebugStringA(errorMsg);
    }
    catch (...) {
        OutputDebugStringA("Unknown exception in SubclassProc");
    }

    return DefSubclassProc(hWnd, uMsg, wParam, lParam);
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

        // Check if this is our message to toggle auto-pairing
        if (msg->message == WM_TOGGLE_AUTO_PAIRING) {
            g_enableAutoPairing = (msg->wParam != 0);
            char debugMsg[100];
            sprintf_s(debugMsg, "Auto-pairing %s\n", g_enableAutoPairing ? "enabled" : "disabled");
            OutputDebugStringA(debugMsg);

            // Mark the message as handled
            msg->message = WM_NULL;
        }
        // Check if this is our message to subclass a window
        else if (msg->message == WM_SUBCLASS_WINDOW) {
            HWND hWndToSubclass = (HWND)msg->wParam;
            HWND callbackWindow = (HWND)msg->lParam;
            
            if (hWndToSubclass && IsWindow(hWndToSubclass)) {
                // Subclass the window, passing callbackWindow as dwRefData
                if (SetWindowSubclass(hWndToSubclass, SubclassProc, SUBCLASS_ID, (DWORD_PTR)callbackWindow)) {
                    char debugMsg[100];
                    sprintf_s(debugMsg, "Successfully subclassed window: %p with callback: %p\n", hWndToSubclass, callbackWindow);
                    OutputDebugStringA(debugMsg);
                   
                } else {
                    char debugMsg[100];
                    sprintf_s(debugMsg, "Failed to subclass window: %p, error: %d\n", hWndToSubclass, GetLastError());
                    OutputDebugStringA(debugMsg);
                }
            } else {
                OutputDebugStringA("Invalid window handle for subclassing");
            }

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
        // Set only the GetMessage hook for the specified thread
        g_getMsgHook = SetWindowsHookEx(WH_GETMESSAGE, GetMsgHook, g_hModule, threadId);

        // Return the hook handle
        return g_getMsgHook;
    }

    __declspec(dllexport) BOOL Unhook() {
        BOOL result = TRUE;

        if (g_getMsgHook != NULL) {
            result = UnhookWindowsHookEx(g_getMsgHook);
            g_getMsgHook = NULL;
        }

        return result;
    }
    
    __declspec(dllexport) BOOL UnsubclassWindow(HWND hWnd) {
        if (!hWnd || !IsWindow(hWnd)) return FALSE;
        
        BOOL result = RemoveWindowSubclass(hWnd, SubclassProc, SUBCLASS_ID);        
        return result;
    }
}
