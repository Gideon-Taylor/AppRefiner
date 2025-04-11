#pragma once

#include "Common.h"
#include "AutoPairing.h"
#include "AutoIndent.h"
#include "EditorManager.h"

// Type definition for hook procedure
typedef LRESULT (CALLBACK *HookProc)(int nCode, WPARAM wParam, LPARAM lParam);

// Subclass procedure for handling window messages
LRESULT CALLBACK SubclassProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData);

// GetMessage hook procedure - for thread messages
LRESULT CALLBACK GetMsgHook(int nCode, WPARAM wParam, LPARAM lParam);

// Export functions
extern "C" {
    __declspec(dllexport) HHOOK SetHook(DWORD threadId);
    __declspec(dllexport) BOOL Unhook();
    __declspec(dllexport) BOOL UnsubclassWindow(HWND hWnd);
}

// Function to handle Scintilla notifications
void HandleScintillaNotification(HWND hwnd, SCNotification* scn, HWND callbackWindow);
