#include "HookManager.h"

// Global variables
HHOOK g_getMsgHook = NULL;
HHOOK g_keyboardHook = NULL;
HMODULE g_hModule = NULL;
bool g_enableAutoPairing = false;  // Flag to control auto-pairing feature
unsigned int g_enabledShortcuts = SHORTCUT_NONE;  // Bit field to control which shortcuts are enabled
HWND g_lastEditorHwnd = NULL;      // Track the last editor HWND that received SCN_CHARADDED
HMODULE g_dllSelfReference = NULL; // Self-reference to prevent DLL unloading
HWND g_callbackWindow = NULL;      // Store callback window for keyboard hook

// Clipboard tracking for paste detection
DWORD g_lastClipboardSequence = 0;
DWORD g_lastSeenClipboardSequence = 0;  // Track the last sequence we processed
bool g_hasUnprocessedCopy = false;      // Track if there's an unprocessed copy operation

// Per-thread open target buffer for Results list interception
const int OPEN_TARGET_BUFFER_SIZE = 0x100;  // 256 characters max
thread_local wchar_t g_openTargetBuffer[OPEN_TARGET_BUFFER_SIZE] = { 0 };


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

            // Check if this is a backspace-specific deletion
            bool isBackspace = (scn->nmhdr.code == SCN_MODIFIED) &&
                              (scn->modificationType & SC_MOD_DELETETEXT) &&
                              (scn->modificationType & SC_PERFORMED_USER) &&
                              (scn->length == 1);  // Single character deletion

            if (isBackspace) {
                // Handle backspace-specific logic with shorter debounce
                EditorManager::HandleBackspaceDeletion(hwnd, callbackWindow);
            }

            // Always notify about general text change event (typing pause)
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

            // Check for ampersand character to trigger variable auto-suggest
            if (scn->ch == '&' && callbackWindow && IsWindow(callbackWindow)) {
                // Get the current position
                int currentPos = SendMessage(hwnd, SCI_GETCURRENTPOS, 0, 0);

                // Check what character comes after the & to avoid triggering when adding & to existing variable names
                // Only trigger autocomplete if followed by whitespace, symbol, or end of document
                int nextCharValue = SendMessage(hwnd, SCI_GETCHARAT, currentPos, 0);

                // Check if next character is whitespace, common symbol, or end of document
                // This handles the case where user is prefixing an existing identifier (e.g., changing "x" to "&x")
                bool shouldTriggerAutocomplete = (nextCharValue <= 0) ||  // End of document or invalid
                                                nextCharValue == ' ' ||   // Space
                                                nextCharValue == '\t' ||  // Tab
                                                nextCharValue == '\r' ||  // Carriage return
                                                nextCharValue == '\n' ||  // Line feed
                                                nextCharValue == '(' ||   // Common symbols
                                                nextCharValue == ')' ||
                                                nextCharValue == '{' ||
                                                nextCharValue == '}' ||
                                                nextCharValue == '[' ||
                                                nextCharValue == ']' ||
                                                nextCharValue == ';' ||
                                                nextCharValue == ',' ||
                                                nextCharValue == '=' ||
                                                nextCharValue == '+' ||
                                                nextCharValue == '-' ||
                                                nextCharValue == '*' ||
                                                nextCharValue == '/' ||
                                                nextCharValue == '<' ||
                                                nextCharValue == '>' ||
                                                nextCharValue == '|' ||
                                                nextCharValue == '&';

                if (shouldTriggerAutocomplete) {
                    // Next character is whitespace, symbol, or we're at end of document - show autocomplete
                    char debugMsg[256];
                    sprintf_s(debugMsg, "Ampersand detected, triggering variable suggestions at position %d (next char code: %d)\n",
                             currentPos, nextCharValue);
                    OutputDebugStringA(debugMsg);

                    // Send the variable suggest message with current position as wParam
                    SendMessage(callbackWindow, WM_AR_VARIABLE_SUGGEST, (WPARAM)currentPos, 0);
                } else {
                    // User is adding & to an existing identifier, don't trigger autocomplete
                    char debugMsg[256];
                    sprintf_s(debugMsg, "Ampersand detected but followed by non-symbol character (code: %d), skipping autocomplete\n", nextCharValue);
                    OutputDebugStringA(debugMsg);
                }
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
                bool isShorthand = false;

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

                        // If auto-pairing is disabled, insert the closing parenthesis
                        if (!g_enableAutoPairing) {
                            // Insert closing parenthesis at current position
                            SendMessage(hwnd, SCI_INSERTTEXT, currentPos, (LPARAM)")");
                            sprintf_s(debugMsg, "Auto-pairing disabled: inserted closing ')' for create( at position %d\n", currentPos);
                            OutputDebugStringA(debugMsg);
                        }

                        // Send the create shorthand message with auto-pairing status as wParam
                        // and current position as lParam
                        SendMessage(callbackWindow, WM_AR_CREATE_SHORTHAND, (WPARAM)g_enableAutoPairing, (LPARAM)currentPos);
                        isShorthand = true;
                    }
                }

                // Check for MsgBox shorthand detection (only if not already a shorthand)
                if (!isShorthand) {
                    const char* msgboxKeyword = "msgbox";
                    int msgboxKeywordLength = 6; // "msgbox" length

                    // Check if we have enough characters before the current position
                    if (currentPos >= msgboxKeywordLength) {
                        // Get the characters before the current position
                        char msgboxBuffer[7] = { 0 }; // "msgbox" + null terminator
                        Sci_TextRange msgboxTr;
                        msgboxTr.chrg.cpMin = currentPos - msgboxKeywordLength - 1;
                        msgboxTr.chrg.cpMax = currentPos - 1; // -1 because current position is after the '('
                        msgboxTr.lpstrText = msgboxBuffer;

                        SendMessage(hwnd, SCI_GETTEXTRANGE, 0, (LPARAM)&msgboxTr);

                        // Convert to lowercase for case-insensitive comparison
                        for (int i = 0; i < msgboxKeywordLength; i++) {
                            msgboxBuffer[i] = tolower(msgboxBuffer[i]);
                        }

                        // Check if the text matches "msgbox"
                        if (strcmp(msgboxBuffer, msgboxKeyword) == 0) {
                            char debugMsg[100];
                            sprintf_s(debugMsg, "Detected 'MsgBox(' pattern at position %d\n", currentPos);
                            OutputDebugStringA(debugMsg);

                            // If auto-pairing is disabled, insert the closing parenthesis
                            if (!g_enableAutoPairing) {
                                // Insert closing parenthesis at current position
                                SendMessage(hwnd, SCI_INSERTTEXT, currentPos, (LPARAM)")");
                                sprintf_s(debugMsg, "Auto-pairing disabled: inserted closing ')' for MsgBox( at position %d\n", currentPos);
                                OutputDebugStringA(debugMsg);
                            }

                            // Send the MsgBox shorthand message with auto-pairing status as wParam
                            // and current position as lParam
                            SendMessage(callbackWindow, WM_AR_MSGBOX_SHORTHAND, (WPARAM)g_enableAutoPairing, (LPARAM)currentPos);
                            isShorthand = true;
                        }
                    }
                }

                // If not a shorthand, send function call tip message
                if (!isShorthand) {
                    SendMessage(callbackWindow, WM_AR_FUNCTION_CALL_TIP, (WPARAM)currentPos, (LPARAM)'(');
                }
            }

            // Check for closing parenthesis for function call tips
            if (scn->ch == ')' && callbackWindow && IsWindow(callbackWindow)) {
                int currentPos = SendMessage(hwnd, SCI_GETCURRENTPOS, 0, 0);
                SendMessage(callbackWindow, WM_AR_FUNCTION_CALL_TIP, (WPARAM)currentPos, (LPARAM)')');
            }

            // Check for comma for function call tips (parameter navigation)
            if (scn->ch == ',' && callbackWindow && IsWindow(callbackWindow)) {
                int currentPos = SendMessage(hwnd, SCI_GETCURRENTPOS, 0, 0);
                SendMessage(callbackWindow, WM_AR_FUNCTION_CALL_TIP, (WPARAM)currentPos, (LPARAM)',');
            }

            // Check for dot character to trigger object member suggestions
            if (scn->ch == '.' && callbackWindow && IsWindow(callbackWindow)) {
                int currentPos = SendMessage(hwnd, SCI_GETCURRENTPOS, 0, 0);

                // Check next character to avoid triggering when adding '.' mid-identifier
                int nextCharValue = SendMessage(hwnd, SCI_GETCHARAT, currentPos, 0);

                // Check if next character is whitespace, common symbol, or end of document
                bool shouldTriggerAutocomplete = (nextCharValue <= 0) ||  // End of document or invalid
                    nextCharValue == ' ' ||   // Space
                    nextCharValue == '\t' ||  // Tab
                    nextCharValue == '\r' ||  // Carriage return
                    nextCharValue == '\n' ||  // Newline
                    nextCharValue == '(' ||   // Opening parenthesis
                    nextCharValue == ')' ||   // Closing parenthesis
                    nextCharValue == '[' ||   // Opening bracket
                    nextCharValue == ']' ||   // Closing bracket
                    nextCharValue == ',' ||   // Comma
                    nextCharValue == ';' ||   // Semicolon
                    nextCharValue == '=' ||   // Equals
                    nextCharValue == '&' ||   // Ampersand
                    nextCharValue == '.' ||   // Dot (chained member access)
                    nextCharValue == '%';     // Percent

                if (shouldTriggerAutocomplete) {
                    // Send the object members message with current position as wParam
                    SendMessage(callbackWindow, WM_AR_OBJECT_MEMBERS, (WPARAM)currentPos, 0);
                }
            }

            // Check for percent character to trigger system variable suggestions
            if (scn->ch == '%' && callbackWindow && IsWindow(callbackWindow)) {
                int currentPos = SendMessage(hwnd, SCI_GETCURRENTPOS, 0, 0);

                // Check next character to avoid triggering when adding '%' mid-identifier
                int nextCharValue = SendMessage(hwnd, SCI_GETCHARAT, currentPos, 0);

                // Check if next character is whitespace, common symbol, or end of document
                bool shouldTriggerAutocomplete = (nextCharValue <= 0) ||  // End of document or invalid
                    nextCharValue == ' ' ||   // Space
                    nextCharValue == '\t' ||  // Tab
                    nextCharValue == '\r' ||  // Carriage return
                    nextCharValue == '\n' ||  // Newline
                    nextCharValue == '(' ||   // Opening parenthesis
                    nextCharValue == ')' ||   // Closing parenthesis
                    nextCharValue == '[' ||   // Opening bracket
                    nextCharValue == ']' ||   // Closing bracket
                    nextCharValue == ',' ||   // Comma
                    nextCharValue == ';' ||   // Semicolon
                    nextCharValue == '=' ||   // Equals
                    nextCharValue == '&' ||   // Ampersand
                    nextCharValue == '.' ||   // Dot
                    nextCharValue == '%';     // Percent

                if (shouldTriggerAutocomplete) {
                    // Send the system variable suggest message with current position as wParam
                    SendMessage(callbackWindow, WM_AR_SYSTEM_VARIABLE_SUGGEST, (WPARAM)currentPos, 0);
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
        else if (scn->nmhdr.code == SCN_AUTOCSELECTION) {
            if (callbackWindow && IsWindow(callbackWindow)) {
                // Output debug info
                char debugMsg[256];
                sprintf_s(debugMsg, "Autocomplete selection: %s\n", scn->text ? scn->text : "NULL");
                OutputDebugStringA(debugMsg);

                // Forward the autocomplete selection to the callback window
                // Note: scn->text contains the selected item text
                SendMessage(callbackWindow, WM_SCN_AUTOCSELECTION, (WPARAM)0, (LPARAM)scn->text);
            }
        }
        else if (scn->nmhdr.code == SCN_AUTOCCOMPLETED) {
            if (callbackWindow && IsWindow(callbackWindow)) {
                // Output debug info
                OutputDebugStringA("Autocomplete completed\n");

                // Forward the autocomplete completed notification to the callback window
                SendMessage(callbackWindow, WM_SCN_AUTOCCOMPLETED, (WPARAM)0, (LPARAM)0);
            }
        }

        // Handle cursor position changes (SCN_UPDATEUI with SC_UPDATE_SELECTION)
        if (scn->nmhdr.code == SCN_UPDATEUI) {
            // Check if the update is related to selection/cursor position
            if (scn->updated & SC_UPDATE_SELECTION) {
                // Notify EditorManager about cursor position change event
                EditorManager::HandleCursorPositionChangeEvent(hwnd, callbackWindow);
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

// Main window subclass procedure for handling keyboard shortcuts
LRESULT CALLBACK MainWindowSubclassProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData) {
    try {
        // Handle WM_NCDESTROY message to remove subclassing
        if (uMsg == WM_NCDESTROY) {
            RemoveWindowSubclass(hWnd, MainWindowSubclassProc, MAIN_WINDOW_SUBCLASS_ID);
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        // Get the callback window from dwRefData
        HWND callbackWindow = (HWND)dwRefData;

        // Handle WM_AR_SET_OPEN_TARGET message for Results list interception
        if (uMsg == WM_AR_SET_OPEN_TARGET) {
            // wParam contains a pointer to the wide string in the remote process buffer
            // lParam contains the character count
            wchar_t* remoteWideString = (wchar_t*)wParam;
            int characterCount = (int)lParam;
            
            // Safety check: ensure character count doesn't exceed buffer size
            if (characterCount > 0 && characterCount < OPEN_TARGET_BUFFER_SIZE && remoteWideString) {
                // Clear the buffer first
                memset(g_openTargetBuffer, 0, sizeof(g_openTargetBuffer));
                
                // Copy the string to our thread-local buffer
                wcsncpy_s(g_openTargetBuffer, OPEN_TARGET_BUFFER_SIZE, remoteWideString, characterCount);
                g_openTargetBuffer[characterCount] = L'\0';
                
                char debugMsg[300];
                sprintf_s(debugMsg, "Open target set: %d characters copied to buffer\n", characterCount);
                OutputDebugStringA(debugMsg);
                
                // Return 1 to indicate success
                return 1;
            } else {
                // Invalid input - clear the buffer
                memset(g_openTargetBuffer, 0, sizeof(g_openTargetBuffer));
                OutputDebugStringA("Invalid open target parameters - buffer cleared");
                
                // Return 0 to indicate failure
                return 0;
            }
        }

        // Handle WM_SET_MAIN_WINDOW_SHORTCUTS message for setting shortcut flags
        if (uMsg == WM_SET_MAIN_WINDOW_SHORTCUTS) {
            g_enabledShortcuts = (unsigned int)wParam;
            char debugMsg[256];
            sprintf_s(debugMsg, "Main window shortcuts set to: %u (CommandPalette: %s, Open: %s, Search: %s, LineSelection: %s)\n", 
                      g_enabledShortcuts,
                      (g_enabledShortcuts & SHORTCUT_COMMAND_PALETTE) ? "On" : "Off",
                      (g_enabledShortcuts & SHORTCUT_OPEN) ? "On" : "Off",
                      (g_enabledShortcuts & SHORTCUT_SEARCH) ? "On" : "Off",
                      (g_enabledShortcuts & SHORTCUT_LINE_SELECTION) ? "On" : "Off");
            OutputDebugStringA(debugMsg);
            
            // Return 1 to indicate success
            return 1;
        }

        // Handle WM_TOGGLE_AUTO_PAIRING message for toggling auto-pairing feature
        if (uMsg == WM_TOGGLE_AUTO_PAIRING) {
            g_enableAutoPairing = (wParam != 0);
            char debugMsg[100];
            sprintf_s(debugMsg, "Auto-pairing %s\n", g_enableAutoPairing ? "enabled" : "disabled");
            OutputDebugStringA(debugMsg);
            
            // Return 1 to indicate success
            return 1;
        }

        // Only process if any shortcuts are enabled
        if (g_enabledShortcuts == SHORTCUT_NONE) {
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        // Handle WM_COMMAND messages (generated by accelerator keys)
        if (uMsg == WM_COMMAND && callbackWindow && IsWindow(callbackWindow)) {
            WORD commandId = LOWORD(wParam);
            
            // Check for common Find/Replace command IDs that MFC applications use
            // These are typical command IDs used by MFC for Find/Replace
            bool shouldIntercept = false;
            char commandType = 0;
            
            // Common MFC command IDs for Find/Replace - only intercept if SHORTCUT_SEARCH is enabled
            if ((g_enabledShortcuts & SHORTCUT_SEARCH) && 
                (commandId == 57636 || commandId == 0xE110)) { // ID_EDIT_FIND variants
                shouldIntercept = true;
                commandType = 'F';
            } else if ((g_enabledShortcuts & SHORTCUT_SEARCH) && 
                      (commandId == 57637 || commandId == 0xE111)) { // ID_EDIT_REPLACE variants
                shouldIntercept = true;
                commandType = 'H';
            } else if ((g_enabledShortcuts & SHORTCUT_SEARCH) && 
                      (commandId == 57638 || commandId == 0xE112)) { // ID_EDIT_REPEAT variants (F3)
                shouldIntercept = true;
                commandType = VK_F3;
            }
            
            if (shouldIntercept) {
                // Convert to our key combination format
                WPARAM modifierFlags = 0;
                if (commandType == 'F' || commandType == 'H') {
                    modifierFlags |= 0x10000; // Ctrl modifier
                }
                
                WPARAM combinedParam = modifierFlags | commandType;
                
                // Forward to C# application
                SendMessage(callbackWindow, WM_AR_KEY_COMBINATION, combinedParam, 3); // lParam = 3 indicates WM_COMMAND source
                
                char debugMsg[100];
                sprintf_s(debugMsg, "WM_COMMAND intercepted: CommandId=%d, converted to key=%c\n", commandId, commandType);
                OutputDebugStringA(debugMsg);
                
                // Return 0 to indicate we handled the message
                return 0;
            }
        }

        // Handle keyboard shortcuts on WM_KEYDOWN and WM_SYSKEYDOWN
        if ((uMsg == WM_KEYDOWN || uMsg == WM_SYSKEYDOWN) && callbackWindow && IsWindow(callbackWindow)) {
            // Check for modifier keys
            bool hasCtrl = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
            bool hasShift = (GetKeyState(VK_SHIFT) & 0x8000) != 0;
            bool hasAlt = (GetKeyState(VK_MENU) & 0x8000) != 0;
            
            // Check for specific shortcuts we want to intercept
            bool shouldIntercept = false;
            
            if ((g_enabledShortcuts & SHORTCUT_SEARCH) && hasCtrl && !hasAlt && wParam == 'F') {
                // Ctrl+F
                shouldIntercept = true;
            } else if ((g_enabledShortcuts & SHORTCUT_SEARCH) && hasCtrl && !hasAlt && wParam == 'H') {
                // Ctrl+H
                shouldIntercept = true;
            } else if ((g_enabledShortcuts & SHORTCUT_SEARCH) && !hasCtrl && !hasAlt && wParam == VK_F3) {
                // F3 (with or without Shift for Find Next/Previous)
                shouldIntercept = true;
            } else if ((g_enabledShortcuts & SHORTCUT_OPEN) && hasCtrl && !hasAlt && wParam == 'O') {
                // Ctrl+O
                shouldIntercept = true;
            } else if ((g_enabledShortcuts & SHORTCUT_COMMAND_PALETTE) && hasCtrl && hasShift && !hasAlt && wParam == 'P') {
                // Ctrl+Shift+P
                shouldIntercept = true;
            }
            
            if (shouldIntercept) {
                // Pack modifier flags into high word of wParam, virtual key code in low word
                WPARAM modifierFlags = 0;
                if (hasCtrl) modifierFlags |= 0x10000;  // Ctrl = bit 16
                if (hasShift) modifierFlags |= 0x20000; // Shift = bit 17
                if (hasAlt) modifierFlags |= 0x40000;   // Alt = bit 18
                
                WPARAM combinedParam = modifierFlags | (wParam & 0xFFFF);
                
                // Forward the key combination to C# application
                SendMessage(callbackWindow, WM_AR_KEY_COMBINATION, combinedParam, 1); // lParam = 1 indicates main window source
                
                char debugMsg[100];
                sprintf_s(debugMsg, "Main window shortcut intercepted: %c (modifiers: %s%s%s)\n", 
                          (char)wParam, 
                          hasCtrl ? "Ctrl " : "",
                          hasShift ? "Shift " : "",
                          hasAlt ? "Alt " : "");
                OutputDebugStringA(debugMsg);
                
                // Return 0 to indicate we handled the message and prevent further processing
                return 0;
            }
        }
    }
    catch (const std::exception& e) {
        char errorMsg[256];
        sprintf_s(errorMsg, "Exception in MainWindowSubclassProc: %s", e.what());
        OutputDebugStringA(errorMsg);
    }
    catch (...) {
        OutputDebugStringA("Unknown exception in MainWindowSubclassProc");
    }

    // Call default subclass procedure for all other messages
    return DefSubclassProc(hWnd, uMsg, wParam, lParam);
}

// Results list view subclass procedure for intercepting item text requests
LRESULT CALLBACK ResultsListSubclassProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData) {
    try {
        // Handle WM_NCDESTROY message to remove subclassing
        if (uMsg == WM_NCDESTROY) {
            RemoveWindowSubclass(hWnd, ResultsListSubclassProc, RESULTS_LIST_SUBCLASS_ID);
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        // Intercept LVM_GETITEMTEXTW (0x1073)
        if (uMsg == 0x1073) { // LVM_GETITEMTEXTW
            LVITEMW* lvItem = (LVITEMW*)lParam;
            
            // Restrict handling to iSubItem == 1 as specified
            if (lvItem && lvItem->iSubItem == 1) {
                // Check if open target buffer contains a non-empty string
                if (g_openTargetBuffer[0] != L'\0') {
                    int bufferLength = wcslen(g_openTargetBuffer);
                    
                    // Copy up to min(bufferLength, cchTextMax - 1) characters
                    int copyLength = min(bufferLength, lvItem->cchTextMax - 1);
                    
                    if (copyLength > 0 && lvItem->pszText) {
                        wcsncpy_s(lvItem->pszText, lvItem->cchTextMax, g_openTargetBuffer, copyLength);
                        lvItem->pszText[copyLength] = L'\0';
                    } else if (lvItem->pszText && lvItem->cchTextMax > 0) {
                        lvItem->pszText[0] = L'\0';
                        copyLength = 0;
                    }
                    
                    // Clear the open target buffer immediately after use
                    memset(g_openTargetBuffer, 0, sizeof(g_openTargetBuffer));
                    
                    char debugMsg[200];
                    sprintf_s(debugMsg, "LVM_GETITEMTEXTW intercepted: iSubItem=%d, returned %d characters\n", 
                              lvItem->iSubItem, copyLength);
                    OutputDebugStringA(debugMsg);
                    
                    // Return the number of characters written, excluding terminator
                    return copyLength;
                } else {
                    // Buffer is empty, return empty string
                    if (lvItem->pszText && lvItem->cchTextMax > 0) {
                        lvItem->pszText[0] = L'\0';
                    }
                    return 0;
                }
            }
        }
    }
    catch (const std::exception& e) {
        char errorMsg[256];
        sprintf_s(errorMsg, "Exception in ResultsListSubclassProc: %s", e.what());
        OutputDebugStringA(errorMsg);
    }
    catch (...) {
        OutputDebugStringA("Unknown exception in ResultsListSubclassProc");
    }

    // Call default subclass procedure for all other messages
    return DefSubclassProc(hWnd, uMsg, wParam, lParam);
}

// Keyboard hook procedure - for intercepting keyboard messages before MFC processing
LRESULT CALLBACK KeyboardHook(int nCode, WPARAM wParam, LPARAM lParam) {
    // Always call the next hook if code is less than zero
    if (nCode < 0) {
        return CallNextHookEx(g_keyboardHook, nCode, wParam, lParam);
    }
    
    try {
        // Only process if any shortcuts are enabled and we have a callback window
        if (g_enabledShortcuts == SHORTCUT_NONE || !g_callbackWindow || !IsWindow(g_callbackWindow)) {
            return CallNextHookEx(g_keyboardHook, nCode, wParam, lParam);
        }

        // HC_ACTION means we should process the keystroke
        if (nCode == HC_ACTION) {
            // wParam contains the virtual key code
            // lParam contains key data (bit 31 = 0 for key down, 1 for key up)
            bool keyDown = (lParam & 0x80000000) == 0;
            
            // Only process key down events
            if (keyDown) {
                // Check for modifier keys
                bool hasCtrl = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
                bool hasShift = (GetKeyState(VK_SHIFT) & 0x8000) != 0;
                bool hasAlt = (GetKeyState(VK_MENU) & 0x8000) != 0;
                
                // Check for specific shortcuts we want to intercept
                bool shouldIntercept = false;
                
                if ((g_enabledShortcuts & SHORTCUT_SEARCH) && hasCtrl && !hasAlt && wParam == 'F') {
                    // Ctrl+F
                    shouldIntercept = true;
                } else if ((g_enabledShortcuts & SHORTCUT_SEARCH) && hasCtrl && !hasAlt && wParam == 'H') {
                    // Ctrl+H
                    shouldIntercept = true;
                } else if ((g_enabledShortcuts & SHORTCUT_SEARCH) && !hasCtrl && !hasAlt && wParam == VK_F3) {
                    // F3 (with or without Shift for Find Next/Previous)
                    shouldIntercept = true;
                } else if ((g_enabledShortcuts & SHORTCUT_OPEN) && hasCtrl && !hasAlt && wParam == 'O') {
                    // Ctrl+O
                    shouldIntercept = true;
                }
                else if (!hasCtrl && !hasAlt && wParam == VK_F12) {
                    // F12 for "go to definition" (always intercept if any shortcuts are enabled)
                    shouldIntercept = true;
                }
                else if ((g_enabledShortcuts & SHORTCUT_COMMAND_PALETTE) && hasCtrl && hasShift && wParam == 'P') {
                    // Ctrl Shift P is for opening the command palette
                    shouldIntercept = true;
                }
                else if ((g_enabledShortcuts & SHORTCUT_LINE_SELECTION) && hasShift && !hasCtrl && !hasAlt && (wParam == VK_UP || wParam == VK_DOWN)) {
                    // Shift + Up/Down arrow for line selection extension in Scintilla
                    HWND focusedWindow = GetFocus();
                    
                    if (focusedWindow && IsWindow(focusedWindow)) {
                        char className[256] = { 0 };
                        char windowTitle[256] = { 0 };
                        GetClassNameA(focusedWindow, className, sizeof(className));
                        GetWindowTextA(focusedWindow, windowTitle, sizeof(windowTitle));
                        
                        char debugMsg[512];
                        sprintf_s(debugMsg, "Shift+%s detected - Focused HWND: %p, Title: '%s', Class: '%s'\n",
                                  wParam == VK_UP ? "Up" : "Down",
                                  focusedWindow, 
                                  windowTitle[0] ? windowTitle : "(no title)",
                                  className);
                        OutputDebugStringA(debugMsg);
                        
                        // Check if it's a Scintilla control
                        if (strncmp(className, "Scintilla", 9) == 0) {
                            // Send the appropriate Scintilla message
                            UINT sciMessage = (wParam == VK_UP) ? SCI_LINEUPEXTEND : SCI_LINEDOWNEXTEND;
                            SendMessage(focusedWindow, sciMessage, 0, 0);
                            
                            sprintf_s(debugMsg, "Sent %s to Scintilla HWND: %p\n",
                                      wParam == VK_UP ? "SCI_LINEUPEXTEND" : "SCI_LINEDOWNEXTEND",
                                      focusedWindow);
                            OutputDebugStringA(debugMsg);
                            
                            // Intercept this keystroke to prevent default handling
                            shouldIntercept = true;
                        } else {
                            sprintf_s(debugMsg, "Not a Scintilla window - no message sent\n");
                            OutputDebugStringA(debugMsg);
                        }
                    } else {
                        OutputDebugStringA("No valid focused window found\n");
                    }
                }
                else if (hasCtrl && wParam == ' ') {
					// Ctrl+Space for triggering autocomplete
                    shouldIntercept = true;
                }

                
                if (shouldIntercept) {
                    // Pack modifier flags into high word of wParam, virtual key code in low word
                    WPARAM modifierFlags = 0;
                    if (hasCtrl) modifierFlags |= 0x10000;  // Ctrl = bit 16
                    if (hasShift) modifierFlags |= 0x20000; // Shift = bit 17
                    if (hasAlt) modifierFlags |= 0x40000;   // Alt = bit 18
                    
                    WPARAM combinedParam = modifierFlags | (wParam & 0xFFFF);
                    
                    // Forward the key combination to C# application
                    SendMessage(g_callbackWindow, WM_AR_KEY_COMBINATION, combinedParam, 2); // lParam = 2 indicates keyboard hook source
                    
                    char debugMsg[100];
                    sprintf_s(debugMsg, "Keyboard hook intercepted: %c (modifiers: %s%s%s)\n", 
                              (char)wParam, 
                              hasCtrl ? "Ctrl " : "",
                              hasShift ? "Shift " : "",
                              hasAlt ? "Alt " : "");
                    OutputDebugStringA(debugMsg);
                    
                    // Return 1 to suppress the keystroke
                    return 1;
                }
            }
        }
    }
    catch (const std::exception& e) {
        char errorMsg[256];
        sprintf_s(errorMsg, "Exception in KeyboardHook: %s", e.what());
        OutputDebugStringA(errorMsg);
    }
    catch (...) {
        OutputDebugStringA("Unknown exception in KeyboardHook");
    }

    return CallNextHookEx(g_keyboardHook, nCode, wParam, lParam);
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

        // Check if this is our message to subclass a window
        if (msg->message == WM_SUBCLASS_SCINTILLA_PARENT_WINDOW) {
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
        // Check if this is our message to subclass main window
        else if (msg->message == WM_SUBCLASS_MAIN_WINDOW) {
            HWND hMainWindow = (HWND)msg->wParam;
            HWND callbackWindow = (HWND)msg->lParam;
            
            if (hMainWindow && IsWindow(hMainWindow)) {
                // Store the callback window for keyboard hook
                g_callbackWindow = callbackWindow;
                
                // Subclass the main window, passing callbackWindow as dwRefData
                SetWindowSubclass(hMainWindow, MainWindowSubclassProc, MAIN_WINDOW_SUBCLASS_ID, (DWORD_PTR)callbackWindow);
                
                char debugMsg[100];
                sprintf_s(debugMsg, "Main window subclassed: HWND=%p, Callback=%p\n", hMainWindow, callbackWindow);
                OutputDebugStringA(debugMsg);
            } else {
                OutputDebugStringA("Invalid main window handle for subclassing");
            }

            // Mark the message as handled
            msg->message = WM_NULL;
        }
        // Check if this is our message to subclass Results list view
        else if (msg->message == WM_AR_SUBCLASS_RESULTS_LIST) {
            HWND hResultsListView = (HWND)msg->wParam;
            HWND callbackWindow = (HWND)msg->lParam;
            
            if (hResultsListView && IsWindow(hResultsListView)) {
                // Subclass the Results list view
                SetWindowSubclass(hResultsListView, ResultsListSubclassProc, RESULTS_LIST_SUBCLASS_ID, (DWORD_PTR)callbackWindow);
                
                char debugMsg[200];
                sprintf_s(debugMsg, "Results list view subclassed: HWND=%p, Callback=%p\n", hResultsListView, callbackWindow);
                OutputDebugStringA(debugMsg);
            } else {
                OutputDebugStringA("Invalid Results list view handle for subclassing");
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

    __declspec(dllexport) HHOOK SetKeyboardHook(DWORD threadId) {
        // Set the keyboard hook for the specified thread
        g_keyboardHook = SetWindowsHookEx(WH_KEYBOARD, KeyboardHook, g_hModule, threadId);

        // Return the hook handle
        return g_keyboardHook;
    }

    __declspec(dllexport) BOOL Unhook() {
        BOOL result = TRUE;

        if (g_getMsgHook != NULL) {
            result = UnhookWindowsHookEx(g_getMsgHook);
            g_getMsgHook = NULL;
        }

        return result;
    }

    __declspec(dllexport) BOOL UnhookKeyboard() {
        BOOL result = TRUE;

        if (g_keyboardHook != NULL) {
            result = UnhookWindowsHookEx(g_keyboardHook);
            g_keyboardHook = NULL;
        }

        return result;
    }
    
    __declspec(dllexport) BOOL UnsubclassWindow(HWND hWnd) {
        if (!hWnd || !IsWindow(hWnd)) return FALSE;
        
        BOOL result = RemoveWindowSubclass(hWnd, SubclassProc, SUBCLASS_ID);        
        return result;
    }
}
