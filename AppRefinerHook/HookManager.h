#pragma once

#include "Common.h"
#include "AutoPairing.h"
#include "AutoIndent.h"
#include "EditorManager.h"

// Type definition for hook procedure
typedef LRESULT (CALLBACK *HookProc)(int nCode, WPARAM wParam, LPARAM lParam);

// Subclass procedure for handling window messages
LRESULT CALLBACK SubclassProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData);

// Scintilla editor subclass procedure for handling escape key and user list visibility
LRESULT CALLBACK ScintillaSubclassProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData);

// Main window subclass procedure for handling keyboard shortcuts
LRESULT CALLBACK MainWindowSubclassProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData);

// GetMessage hook procedure - for thread messages
LRESULT CALLBACK GetMsgHook(int nCode, WPARAM wParam, LPARAM lParam);

// Keyboard hook procedure - for intercepting keyboard messages before MFC processing
LRESULT CALLBACK KeyboardHook(int nCode, WPARAM wParam, LPARAM lParam);

// Export functions
extern "C" {
    __declspec(dllexport) HHOOK SetHook(DWORD threadId);
    __declspec(dllexport) HHOOK SetKeyboardHook(DWORD threadId);
    __declspec(dllexport) BOOL Unhook();
    __declspec(dllexport) BOOL UnhookKeyboard();
    __declspec(dllexport) BOOL UnsubclassWindow(HWND hWnd);
}

// Function to handle Scintilla notifications
void HandleScintillaNotification(HWND hwnd, SCNotification* scn, HWND callbackWindow);
