#pragma once

#include "Common.h"
#include <map>

// Message to be sent when typing pause is detected
#define WM_AR_TYPING_PAUSE 2502 // Using 2502 to follow after existing AR_ messages

// Class for managing editor events like typing pauses and cursor position changes
class EditorManager {
private:
    struct EditorInfo {
        HWND hwndEditor;       // Handle to the editor window
        bool typingActive;     // Flag indicating if typing is active
        HWND callbackWindow;   // Window to notify when typing pauses
        bool cursorPositionActive; // Flag indicating if cursor position tracking is active
        int lastCursorPosition;    // Last known cursor position
        int lastFirstVisibleLine;  // Last known first visible line
    };

    // Map of editor handles to their info
    static std::map<HWND, EditorInfo> s_editorMap;

    // Static timer callbacks (must be static for SetTimer)
    static VOID CALLBACK TypingTimerProc(HWND hwnd, UINT uMsg, UINT_PTR idEvent, DWORD dwTime);
    static VOID CALLBACK CursorPositionTimerProc(HWND hwnd, UINT uMsg, UINT_PTR idEvent, DWORD dwTime);

public:
    // Constants
    static const UINT TYPING_PAUSE_MS = 1000; // Pause duration in milliseconds
    static const UINT_PTR TYPING_TIMER_ID = 1234; // Specific timer ID for typing detection
    static const UINT CURSOR_POSITION_DEBOUNCE_MS = 300; // Debounce duration for cursor position changes
    static const UINT_PTR CURSOR_POSITION_TIMER_ID = 1235; // Specific timer ID for cursor position tracking

    // Initialize the EditorManager
    static void Initialize();

    // Clean up resources
    static void Cleanup();

    // Handle a text change event from a Scintilla editor (typing, deletion, cut, paste)
    static void HandleTextChangeEvent(HWND hwndEditor, HWND callbackWindow);

    // Handle a cursor position change event from a Scintilla editor
    static void HandleCursorPositionChangeEvent(HWND hwndEditor, HWND callbackWindow);

    // Remove tracking for an editor window (e.g., when closed)
    static void RemoveEditor(HWND hwndEditor);
}; 