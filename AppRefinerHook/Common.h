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
#define WM_SUBCLASS_WINDOW (WM_USER + 1003)
// Message to toggle main window shortcuts feature
#define WM_TOGGLE_MAIN_WINDOW_SHORTCUTS (WM_USER + 1006)
// Message to subclass main window
#define WM_SUBCLASS_MAIN_WINDOW (WM_USER + 1005)

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
#define WM_AR_TEXT_PASTED 2506 // Text pasted notification
#define WM_AR_KEY_COMBINATION 2507 // Key combination with modifiers notification
#define WM_AR_MSGBOX_SHORTHAND 2508 // New message for MsgBox shorthand when user types "MsgBox("
#define WM_SCN_USERLIST_SELECTION WM_SCN(SCN_USERLISTSELECTION) // User list selection notification

// Global variables (defined in HookManager.cpp)
extern HHOOK g_getMsgHook;
extern HHOOK g_keyboardHook;
extern HMODULE g_hModule;
extern HMODULE g_dllSelfReference;
extern bool g_enableAutoPairing;
extern bool g_enableMainWindowShortcuts;
extern DWORD g_lastClipboardSequence;
extern DWORD g_lastSeenClipboardSequence;
extern bool g_hasUnprocessedCopy;
extern HWND g_callbackWindow;

// Subclass IDs for our window subclassing
const UINT_PTR SUBCLASS_ID = 1001;
const UINT_PTR SCINTILLA_SUBCLASS_ID = 1002;
const UINT_PTR MAIN_WINDOW_SUBCLASS_ID = 1003;
