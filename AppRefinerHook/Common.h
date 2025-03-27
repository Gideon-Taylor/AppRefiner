#pragma once

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <string>
#include <cctype>
#include <psapi.h>
#include <vector>
#include "Scintilla.h"

// Custom message for setting pipe name
#define WM_SET_CALLBACK_WINDOW (WM_USER + 1001)
// Message to toggle auto-pairing feature
#define WM_TOGGLE_AUTO_PAIRING (WM_USER + 1002)
// Message types for pipe communication
#define MSG_TYPE_DWELL 1

// Scintilla notifications
#define SCN_CHARADDED 2001
#define SCN_MODIFIED 2008
#define SCN_DWELLSTART 2016
#define SCN_DWELLEND 2017

/* TODO define messages with a mask to indicate "this is a scintilla event message" */
#define WM_SCN_EVENT_MASK 0x7000
// Macro to create WM_SCN_ messages by combining SCN_ notifications with the event mask
#define WM_SCN(notification) (WM_SCN_EVENT_MASK | (notification))

// Scintilla notification messages
#define WM_SCN_DWELL_START WM_SCN(SCN_DWELLSTART)
#define WM_SCN_DWELL_END WM_SCN(SCN_DWELLEND)
#define WM_SCN_SAVEPOINT_REACHED WM_SCN(SCN_SAVEPOINTREACHED)

// Global variables (defined in HookManager.cpp)
extern HWND g_callbackWindow;
extern HHOOK g_wndProcHook;
extern HHOOK g_getMsgHook;
extern HMODULE g_hModule;
extern bool g_enableAutoPairing;
