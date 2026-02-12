#pragma once

// Make sure CALLBACK is defined before any function declarations
#ifndef CALLBACK
#define CALLBACK __stdcall
#endif

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <string>
#include <cctype>
#include <psapi.h>
#include <vector>
#include <Commctrl.h>  // For SetWindowSubclass
#include "Scintilla.h"

// Message to toggle auto-pairing feature
#define WM_TOGGLE_AUTO_PAIRING (WM_USER + 1002)
// Message to subclass a window
#define WM_SUBCLASS_SCINTILLA_PARENT_WINDOW (WM_USER + 1003)
// Message to set main window shortcuts feature (now using bit field)
#define WM_SET_MAIN_WINDOW_SHORTCUTS (WM_USER + 1006)
// Message to subclass main window
#define WM_SUBCLASS_MAIN_WINDOW (WM_USER + 1005)
// Message to subclass Results list view
#define WM_AR_SUBCLASS_RESULTS_LIST (WM_USER + 1007)
// Message to set open target for Results list interception
#define WM_AR_SET_OPEN_TARGET (WM_USER + 1008)
// Message to load Scintilla DLL into the process
#define WM_LOAD_SCINTILLA_DLL (WM_USER + 1009)
// Messages to set editor features from AppRefiner
// wParam = Scintilla editor HWND, lParam = 1 (enable) or 0 (disable)
#define WM_AR_SET_MINIMAP (WM_USER + 1010)
#define WM_AR_SET_PARAM_NAMES (WM_USER + 1011)

/* TODO define messages with a mask to indicate "this is a scintilla event message" */
#define WM_SCN_EVENT_MASK 0x7000
// Macro to create WM_SCN_ messages by combining SCN_ notifications with the event mask
#define WM_SCN(notification) (WM_SCN_EVENT_MASK | (notification))

// Scintilla notification messages
#define WM_SCN_DWELL_START WM_SCN(SCN_DWELLSTART)
#define WM_SCN_DWELL_END WM_SCN(SCN_DWELLEND)
#define WM_SCN_SAVEPOINT_REACHED WM_SCN(SCN_SAVEPOINTREACHED)
#define WM_AR_APP_PACKAGE_SUGGEST 2500 // New message for app package auto-suggest when colon is typed
#define WM_AR_CREATE_SHORTHAND 2501 // New message for create shorthand when user types "create("
#define WM_AR_TYPING_PAUSE 2502 // New message for typing pause detection
#define WM_AR_BEFORE_DELETE_ALL 2503 // Before delete all notification
#define WM_AR_FOLD_MARGIN_CLICK 2504 // Fold margin click notification
#define WM_AR_CONCAT_SHORTHAND 2505 // Concat shorthand notification
#define WM_AR_INSERT_CHECK 2506 // Text insert check notification (can change the text before insert)
#define WM_AR_KEY_COMBINATION 2507 // Key combination with modifiers notification
#define WM_AR_MSGBOX_SHORTHAND 2508 // New message for MsgBox shorthand when user types "MsgBox("
#define WM_AR_VARIABLE_SUGGEST 2509 // New message for variable auto-suggest when & is typed
#define WM_AR_CURSOR_POSITION_CHANGED 2510 // Cursor position changed notification (debounced)
#define WM_AR_FUNCTION_CALL_TIP 2511 // Function call tip notification for '(', ')', and ',' characters
#define WM_AR_OBJECT_MEMBERS 2512 // Object member suggestions when '.' is typed
#define WM_AR_SYSTEM_VARIABLE_SUGGEST 2513 // System variable suggestions when '%' is typed
#define WM_AR_SCINTILLA_ALREADY_LOADED 2514 // Scintilla DLL is already loaded
#define WM_AR_SCINTILLA_LOAD_SUCCESS 2515   // Scintilla DLL loaded successfully
#define WM_AR_SCINTILLA_LOAD_FAILED 2516    // Scintilla DLL load failed (wParam contains GetLastError)
#define WM_AR_SCINTILLA_IN_USE 2517         // Scintilla DLL in use (active windows exist, cannot replace)
#define WM_AR_SCINTILLA_NOT_FOUND 2518      // Scintilla DLL file not found at specified path (wParam=(major<<16)|minor, lParam=(build<<16)|revision)
#define WM_AR_COMBO_BUTTON_CLICKED 2519     // ComboBox button clicked notification
#define WM_AR_CONTEXT_MENU_OPTION 2520      // Context menu option selected (wParam=option ID, lParam=toggle state for checkboxes or 0)
#define WM_SCN_USERLIST_SELECTION WM_SCN(SCN_USERLISTSELECTION) // User list selection notification

// Context menu option IDs (for WM_AR_CONTEXT_MENU_OPTION wParam)
#define IDM_COMMAND_PALETTE 1001
#define IDM_MINIMAP 1002
#define IDM_PARAM_NAMES 1003
#define WM_SCN_AUTOCSELECTION WM_SCN(SCN_AUTOCSELECTION) // Autocompletion selection notification
#define WM_SCN_AUTOCCOMPLETED WM_SCN(SCN_AUTOCCOMPLETED) // Autocompletion completed notification

// Global variables (defined in HookManager.cpp)
extern HHOOK g_getMsgHook;
extern HHOOK g_keyboardHook;
extern HMODULE g_hModule;
extern HMODULE g_dllSelfReference;
extern bool g_enableAutoPairing;
// Bit field for shortcut types
enum ShortcutType : unsigned int {
    SHORTCUT_NONE = 0,
    SHORTCUT_COMMAND_PALETTE = 1 << 0,  // Always enabled - Ctrl+Shift+P
    SHORTCUT_OPEN = 1 << 1,             // Override Ctrl+O
    SHORTCUT_SEARCH = 1 << 2,           // Override Ctrl+F, Ctrl+H, F3
    SHORTCUT_LINE_SELECTION = 1 << 3,   // Override Shift+Up/Down for line selection
    SHORTCUT_ALL = SHORTCUT_COMMAND_PALETTE | SHORTCUT_OPEN | SHORTCUT_SEARCH | SHORTCUT_LINE_SELECTION
};

extern unsigned int g_enabledShortcuts;
extern DWORD g_lastClipboardSequence;
extern DWORD g_lastSeenClipboardSequence;
extern bool g_hasUnprocessedCopy;
extern HWND g_callbackWindow;

// Subclass IDs for our window subclassing
const UINT_PTR SUBCLASS_ID = 1001;
const UINT_PTR SCINTILLA_SUBCLASS_ID = 1002;
const UINT_PTR MAIN_WINDOW_SUBCLASS_ID = 1003;
const UINT_PTR RESULTS_LIST_SUBCLASS_ID = 1004;
