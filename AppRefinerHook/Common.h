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
#define WM_SCN_USERLIST_SELECTION WM_SCN(SCN_USERLISTSELECTION) // User list selection notification

// Global variables (defined in HookManager.cpp)
extern HHOOK g_getMsgHook;
extern HMODULE g_hModule;
extern HMODULE g_dllSelfReference;
extern bool g_enableAutoPairing;
