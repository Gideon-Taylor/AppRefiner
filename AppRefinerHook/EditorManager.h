#pragma once

#include "Common.h"
#include <map>

// Message to be sent when typing pause is detected
#define WM_AR_TYPING_PAUSE 2502 // Using 2502 to follow after existing AR_ messages

// Class for managing editor events like typing pauses
class EditorManager {
private:
    struct EditorInfo {
        HWND hwndEditor;       // Handle to the editor window
        bool typingActive;     // Flag indicating if typing is active
        HWND callbackWindow;   // Window to notify when typing pauses
    };
    
    // Map of editor handles to their info
    static std::map<HWND, EditorInfo> s_editorMap;
    
    // Static timer callback (must be static for SetTimer)
    static VOID CALLBACK TypingTimerProc(HWND hwnd, UINT uMsg, UINT_PTR idEvent, DWORD dwTime);

public:
    // Constants
    static const UINT TYPING_PAUSE_MS = 1000; // Pause duration in milliseconds
    static const UINT_PTR TYPING_TIMER_ID = 1234; // Specific timer ID for typing detection

    // Initialize the EditorManager
    static void Initialize();
    
    // Clean up resources
    static void Cleanup();
    
    // Handle a text change event from a Scintilla editor (typing, deletion, cut, paste)
    static void HandleTextChangeEvent(HWND hwndEditor, HWND callbackWindow);
    
    // Remove tracking for an editor window (e.g., when closed)
    static void RemoveEditor(HWND hwndEditor);
}; 