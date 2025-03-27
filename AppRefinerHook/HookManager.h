#pragma once

#include "Common.h"
#include "AutoPairing.h"
#include "AutoIndent.h"

// Type definition for hook procedure
typedef LRESULT (CALLBACK *HookProc)(int nCode, WPARAM wParam, LPARAM lParam);

// WndProc hook procedure - for window messages
LRESULT CALLBACK WndProcHook(int nCode, WPARAM wParam, LPARAM lParam);

// GetMessage hook procedure - for thread messages
LRESULT CALLBACK GetMsgHook(int nCode, WPARAM wParam, LPARAM lParam);

// Export functions
extern "C" {
    __declspec(dllexport) HHOOK SetHook(DWORD threadId);
    __declspec(dllexport) BOOL Unhook();
}
