#include "EditorManager.h"

// Initialize the static member
std::map<HWND, EditorManager::EditorInfo> EditorManager::s_editorMap;

// Timer callback function
VOID CALLBACK EditorManager::TypingTimerProc(HWND hwnd, UINT uMsg, UINT_PTR idEvent, DWORD dwTime) {
    // When the timer fires, hwnd will be the Scintilla editor HWND
    // and idEvent will be TYPING_TIMER_ID
    
    // Make sure this is our timer ID
    if (idEvent != TYPING_TIMER_ID) {
        return;
    }
    
    // Find the editor info for this window
    auto it = s_editorMap.find(hwnd);
    if (it != s_editorMap.end()) {
        auto& info = it->second;
        
        // Process typing pause if typing was active
        if (info.typingActive) {
            info.typingActive = false;
            
            // Validate the callback window still exists
            if (IsWindow(info.callbackWindow)) {
                // Get current position in the editor
                int currentPos = SendMessage(hwnd, SCI_GETCURRENTPOS, 0, 0);
                
                // Get line number from position
                int currentLine = SendMessage(hwnd, SCI_LINEFROMPOSITION, currentPos, 0);
                
                // Send message to callback window with position as wParam and line as lParam
                PostMessage(info.callbackWindow, WM_AR_TYPING_PAUSE, (WPARAM)currentPos, (LPARAM)currentLine);
                
                // Debug output
                char debugMsg[256];
                sprintf_s(debugMsg, "Typing pause detected at position %d, line %d for editor: 0x%p", 
                         currentPos, currentLine, hwnd);
                OutputDebugStringA(debugMsg);
            }
        }
    }
}

// Initialize the EditorManager
void EditorManager::Initialize() {
    // Clear the map just in case
    s_editorMap.clear();
}

// Clean up resources
void EditorManager::Cleanup() {
    // Kill all timers for each editor
    for (auto& pair : s_editorMap) {
        KillTimer(pair.first, TYPING_TIMER_ID);
        KillTimer(pair.first, CURSOR_POSITION_TIMER_ID);
    }

    // Clear the map
    s_editorMap.clear();
}

// Handle a text change event from a Scintilla editor (typing, deletion, cut, paste)
void EditorManager::HandleTextChangeEvent(HWND hwndEditor, HWND callbackWindow) {
    // Check for valid editor window
    if (!hwndEditor || !IsWindow(hwndEditor)) {
        OutputDebugStringA("Invalid editor window handle in HandleTextChangeEvent");
        return;
    }
    
    // Get or create editor info
    EditorInfo& info = s_editorMap[hwndEditor];
    info.hwndEditor = hwndEditor;
    info.typingActive = true;
    info.callbackWindow = callbackWindow;
    
    // Set or reset the timer using the editor window handle
    // No need to explicitly kill the previous timer - SetTimer
    // will automatically replace it since we're using the same ID
    if (!SetTimer(hwndEditor, TYPING_TIMER_ID, TYPING_PAUSE_MS, TypingTimerProc)) {
        // Timer creation failed
        char errorMsg[256];
        sprintf_s(errorMsg, "Failed to create typing pause timer for editor: 0x%p", hwndEditor);
        OutputDebugStringA(errorMsg);
    }
}

// Remove tracking for an editor window
void EditorManager::RemoveEditor(HWND hwndEditor) {
    auto it = s_editorMap.find(hwndEditor);
    if (it != s_editorMap.end()) {
        // Kill all timers for this editor
        KillTimer(hwndEditor, TYPING_TIMER_ID);
        KillTimer(hwndEditor, CURSOR_POSITION_TIMER_ID);

        // Remove the editor from the map
        s_editorMap.erase(it);
    }
}

// Timer callback function for cursor position changes
VOID CALLBACK EditorManager::CursorPositionTimerProc(HWND hwnd, UINT uMsg, UINT_PTR idEvent, DWORD dwTime) {
    // When the timer fires, hwnd will be the Scintilla editor HWND
    // and idEvent will be CURSOR_POSITION_TIMER_ID

    // Make sure this is our timer ID
    if (idEvent != CURSOR_POSITION_TIMER_ID) {
        return;
    }

    // Find the editor info for this window
    auto it = s_editorMap.find(hwnd);
    if (it != s_editorMap.end()) {
        auto& info = it->second;

        // Process cursor position change if tracking was active
        if (info.cursorPositionActive) {
            info.cursorPositionActive = false;

            // Validate the callback window still exists
            if (IsWindow(info.callbackWindow)) {
                // Get current cursor position
                int currentPos = SendMessage(hwnd, SCI_GETCURRENTPOS, 0, 0);

                // Get first visible line
                int firstVisibleLine = SendMessage(hwnd, SCI_GETFIRSTVISIBLELINE, 0, 0);

                // Only send notification if position actually changed
                if (currentPos != info.lastCursorPosition || firstVisibleLine != info.lastFirstVisibleLine) {
                    // Update last known values
                    info.lastCursorPosition = currentPos;
                    info.lastFirstVisibleLine = firstVisibleLine;

                    // Send message to callback window with firstVisibleLine as wParam and cursorPos as lParam
                    PostMessage(info.callbackWindow, WM_AR_CURSOR_POSITION_CHANGED, (WPARAM)firstVisibleLine, (LPARAM)currentPos);

                    // Debug output
                    char debugMsg[256];
                    sprintf_s(debugMsg, "Cursor position changed: line %d, position %d for editor: 0x%p",
                             firstVisibleLine, currentPos, hwnd);
                    OutputDebugStringA(debugMsg);
                }
            }
        }
    }
}

// Handle a cursor position change event from a Scintilla editor
void EditorManager::HandleCursorPositionChangeEvent(HWND hwndEditor, HWND callbackWindow) {
    // Check for valid editor window
    if (!hwndEditor || !IsWindow(hwndEditor)) {
        OutputDebugStringA("Invalid editor window handle in HandleCursorPositionChangeEvent");
        return;
    }

    // Get or create editor info
    EditorInfo& info = s_editorMap[hwndEditor];
    info.hwndEditor = hwndEditor;
    info.cursorPositionActive = true;
    info.callbackWindow = callbackWindow;

    // Initialize last known values if this is a new entry
    if (info.lastCursorPosition == 0 && info.lastFirstVisibleLine == 0) {
        info.lastCursorPosition = -1;  // Force initial update
        info.lastFirstVisibleLine = -1;
    }

    // Set or reset the timer using the editor window handle
    // SetTimer will automatically replace previous timer with the same ID
    if (!SetTimer(hwndEditor, CURSOR_POSITION_TIMER_ID, CURSOR_POSITION_DEBOUNCE_MS, CursorPositionTimerProc)) {
        // Timer creation failed
        char errorMsg[256];
        sprintf_s(errorMsg, "Failed to create cursor position timer for editor: 0x%p", hwndEditor);
        OutputDebugStringA(errorMsg);
    }
} 