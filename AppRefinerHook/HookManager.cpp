#include "HookManager.h"

// Global variables
HHOOK g_getMsgHook = NULL;
HMODULE g_hModule = NULL;
bool g_enableAutoPairing = false;  // Flag to control auto-pairing feature
HWND g_lastEditorHwnd = NULL;      // Track the last editor HWND that received SCN_CHARADDED
HMODULE g_dllSelfReference = NULL; // Self-reference to prevent DLL unloading

// Clipboard tracking for paste detection
DWORD g_lastClipboardSequence = 0;
DWORD g_lastSeenClipboardSequence = 0;  // Track the last sequence we processed
bool g_hasUnprocessedCopy = false;      // Track if there's an unprocessed copy operation


// Function to check for unprocessed copy operation
bool HasUnprocessedCopyOperation() {
    DWORD currentSequence = GetClipboardSequenceNumber();
    
    // Check if clipboard sequence changed (indicating new copy/cut operation)
    if (currentSequence != g_lastClipboardSequence) {
        g_lastClipboardSequence = currentSequence;
        g_hasUnprocessedCopy = true;
        
        char debugMsg[100];
        sprintf_s(debugMsg, "New clipboard activity detected. Sequence: %lu\n", currentSequence);
        OutputDebugStringA(debugMsg);
    }
    
    return g_hasUnprocessedCopy;
}

// Function to mark copy operation as processed (called after paste detection)
void MarkCopyOperationProcessed() {
    g_hasUnprocessedCopy = false;
    g_lastSeenClipboardSequence = g_lastClipboardSequence;
    
    char debugMsg[100];
    sprintf_s(debugMsg, "Copy operation marked as processed. Sequence: %lu\n", g_lastSeenClipboardSequence);
    OutputDebugStringA(debugMsg);
}

// Function to handle Scintilla notifications
void HandleScintillaNotification(HWND hwnd, SCNotification* scn, HWND callbackWindow) {
    if (!scn || !hwnd || !IsWindow(hwnd)) return;
    
    try {
        char debugMsg[256];
        
        /* Check if we are deleting the entire document */
        if (scn->nmhdr.code == SCN_MODIFIED) {

            if (scn->modificationType == (SC_MOD_BEFOREDELETE | SC_PERFORMED_USER)) {
				sprintf_s(debugMsg, "SCN_MODIFIED: SC_MOD_BEFOREDELETE detected\n");
				OutputDebugStringA(debugMsg);
                /* Get document length */
                int docLength = SendMessage(scn->nmhdr.hwndFrom, SCI_GETLENGTH, 0, 0);
				sprintf_s(debugMsg, "Document length: %d\n", docLength);
				OutputDebugStringA(debugMsg);

				sprintf_s(debugMsg, "Delete length: %p, position: %p\n", scn->length, scn->position);
				OutputDebugStringA(debugMsg);
                /* are we about to delete the entire thing (ie, position == 0 and length == doc length */
                if (scn->position == 0 && scn->length == docLength) {
                    // Notify the callback window about the deletion
					sprintf_s(debugMsg, "Sending WM_AR_BEFORE_DELETE_ALL message to callback window: %p\n", callbackWindow);
                    SendMessage(callbackWindow, WM_AR_BEFORE_DELETE_ALL, (WPARAM)0, (LPARAM)docLength);
                }
            }

        }

        if (scn->nmhdr.code == SCN_MARGINCLICK) {
			// Check if the margin clicked is the one we are interested in
            sprintf_s(debugMsg, "Margin Click: %d Position:%p\n", scn->margin, scn->position); 
			if (scn->margin == 2) {
				// Notify EditorManager about the margin click event
                // Send the app package suggest message with current position as wParam
                SendMessage(callbackWindow, WM_AR_FOLD_MARGIN_CLICK, scn->position , 0);
			}
        }

        // Handle paste operations specifically
        if (scn->nmhdr.code == SCN_MODIFIED && 
            (scn->modificationType & SC_MOD_INSERTTEXT) && 
            (scn->modificationType & SC_PERFORMED_USER)) {
            
            // Check if this is likely a paste operation
            // Criteria: 1) Multi-character insert (more than typical typing)
            //          2) There's an unprocessed copy operation available
            //          3) Insert size suggests paste rather than single character or newline
            if (scn->length > 5 && HasUnprocessedCopyOperation()) {
                sprintf_s(debugMsg, "Detected paste operation: length=%d, position=%d\n", scn->length, scn->position);
                OutputDebugStringA(debugMsg);
                
                // Mark the copy operation as processed so we don't trigger on subsequent edits
                MarkCopyOperationProcessed();
                
                // Send paste notification to callback window
                SendMessage(callbackWindow, WM_AR_TEXT_PASTED, (WPARAM)scn->position, (LPARAM)scn->length);
            }
        }

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
                    for (int i = 0; i < keywordLength; i++) {
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

            // Check for += shorthand
            if (scn->ch == '=' && callbackWindow && IsWindow(callbackWindow)) {
                int currentPos = SendMessage(hwnd, SCI_GETCURRENTPOS, 0, 0); // Position after '='

                // currentPos is 0-indexed. We need at least two characters for "+="
                // If currentPos is 1 (meaning '=' is at index 0), currentPos - 2 is invalid.
                // If currentPos is 2 (meaning '=' is at index 1), currentPos - 2 is 0 (valid for '+').
                if (currentPos >= 2) { 
                    char charBeforeEquals = (char)SendMessage(hwnd, SCI_GETCHARAT, currentPos - 2, 0); // Get char at pos of expected '+'

                    /* Support += -= and |= */
                    if (charBeforeEquals == '+' || charBeforeEquals == '-' || charBeforeEquals == '|') {
                        char debugMsg[100];
                        sprintf_s(debugMsg, "Detected '+=' pattern at position %d (char: '%c')\n", currentPos, scn->ch);
                        OutputDebugStringA(debugMsg);

                        // Send the concat shorthand message
                        // WM_AR_CONCAT_SHORTHAND would need to be defined in Common.h or similar
                        SendMessage(callbackWindow, WM_AR_CONCAT_SHORTHAND, (WPARAM)charBeforeEquals, (LPARAM)currentPos);
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

// Scintilla editor subclass procedure for handling escape key and key combinations
LRESULT CALLBACK ScintillaSubclassProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData) {
    try {
        // Handle WM_NCDESTROY message to remove subclassing
        if (uMsg == WM_NCDESTROY) {
            RemoveWindowSubclass(hWnd, ScintillaSubclassProc, SCINTILLA_SUBCLASS_ID);
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        // Get the callback window from dwRefData
        HWND callbackWindow = (HWND)dwRefData;

        // Handle escape key on WM_KEYUP to dismiss UserList
        if (uMsg == WM_KEYUP && wParam == VK_ESCAPE) {
            // Check if a user list is currently active
            LRESULT userListActive = SendMessage(hWnd, SCI_AUTOCACTIVE, 0, 0);
            
            if (userListActive) {
                // Cancel the user list/autocompletion
                SendMessage(hWnd, SCI_AUTOCCANCEL, 0, 0);
                
                // Return 0 to indicate we handled the message and prevent further processing
                return 0;
            }
        }

        // Handle key combinations with modifier keys on WM_KEYUP
        if ((uMsg == WM_KEYUP || uMsg == WM_SYSKEYUP) && callbackWindow && IsWindow(callbackWindow)) {
            // Check for modifier keys
            bool hasCtrl = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
            bool hasShift = (GetKeyState(VK_SHIFT) & 0x8000) != 0;
            bool hasAlt = (GetKeyState(VK_MENU) & 0x8000) != 0;
            
            // Only forward if at least one modifier key is pressed
            if (hasCtrl || hasShift || hasAlt) {
                // Pack modifier flags into high word of wParam, virtual key code in low word
                WPARAM modifierFlags = 0;
                if (hasCtrl) modifierFlags |= 0x10000;  // Ctrl = bit 16
                if (hasShift) modifierFlags |= 0x20000; // Shift = bit 17
                if (hasAlt) modifierFlags |= 0x40000;   // Alt = bit 18
                
                WPARAM combinedParam = modifierFlags | (wParam & 0xFFFF);
                
                // Forward the key combination to C# application
                SendMessage(callbackWindow, WM_AR_KEY_COMBINATION, combinedParam, 0);
            }
        }
    }
    catch (const std::exception& e) {
        char errorMsg[256];
        sprintf_s(errorMsg, "Exception in ScintillaSubclassProc: %s", e.what());
        OutputDebugStringA(errorMsg);
    }
    catch (...) {
        OutputDebugStringA("Unknown exception in ScintillaSubclassProc");
    }

    // Call default subclass procedure for all other messages
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
                // Subclass the parent window, passing callbackWindow as dwRefData
                SetWindowSubclass(hWndToSubclass, SubclassProc, SUBCLASS_ID, (DWORD_PTR)callbackWindow);
                
                // Now find and subclass the child Scintilla editor window
                HWND scintillaChild = FindWindowExA(hWndToSubclass, NULL, "Scintilla", NULL);
                if (scintillaChild && IsWindow(scintillaChild)) {
                    SetWindowSubclass(scintillaChild, ScintillaSubclassProc, SCINTILLA_SUBCLASS_ID, (DWORD_PTR)callbackWindow);
                } else {
                    // If direct child search fails, try recursive search
                    struct FindScintillaData {
                        HWND scintillaHwnd;
                    } findData = { NULL };
                    
                    // Enumerate child windows to find Scintilla editor
                    EnumChildWindows(hWndToSubclass, [](HWND hWnd, LPARAM lParam) -> BOOL {
                        FindScintillaData* data = (FindScintillaData*)lParam;
                        char className[256] = { 0 };
                        if (GetClassNameA(hWnd, className, sizeof(className)) > 0) {
                            if (strncmp(className, "Scintilla", 9) == 0) {
                                data->scintillaHwnd = hWnd;
                                return FALSE; // Stop enumeration
                            }
                        }
                        return TRUE; // Continue enumeration
                    }, (LPARAM)&findData);
                    
                    if (findData.scintillaHwnd && IsWindow(findData.scintillaHwnd)) {
                        SetWindowSubclass(findData.scintillaHwnd, ScintillaSubclassProc, SCINTILLA_SUBCLASS_ID, (DWORD_PTR)callbackWindow);
                    }
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
