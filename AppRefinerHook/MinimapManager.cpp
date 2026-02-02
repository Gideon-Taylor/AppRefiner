#include "MinimapManager.h"
#include "MinimapOverlay.h"
#include "HookManager.h"
#include <stdio.h>

// Property names for storing minimap state
static const wchar_t* MINIMAP_WINDOW_PROP = L"AR_MinimapHwnd";
static const wchar_t* MINIMAP_ENABLED_PROP = L"AR_MinimapEnabled";

// Window class name for minimap windows
static const wchar_t* MINIMAP_WINDOW_CLASS = L"AppRefinerMinimap";

// External references
extern HMODULE g_hModule;
extern HWND g_minimapDragHwnd;
extern bool g_isMinimapDragging;

// Forward declaration of minimap window procedure (defined in HookManager.cpp)
LRESULT CALLBACK MinimapWindowProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam);

bool MinimapManager::IsMinimapEnabled(HWND scintillaHwnd)
{
    if (!scintillaHwnd || !IsWindow(scintillaHwnd)) {
        return false;
    }

    return (GetPropW(scintillaHwnd, MINIMAP_ENABLED_PROP) != NULL);
}

HWND MinimapManager::GetMinimapWindow(HWND scintillaHwnd)
{
    if (!scintillaHwnd || !IsWindow(scintillaHwnd)) {
        return NULL;
    }

    return (HWND)GetPropW(scintillaHwnd, MINIMAP_WINDOW_PROP);
}

HWND MinimapManager::CreateMinimapWindow(HWND parentHwnd, HWND scintillaHwnd)
{
    if (!parentHwnd || !scintillaHwnd) {
        return NULL;
    }

    // Register window class if not already registered
    WNDCLASSW wc = { 0 };
    wc.lpfnWndProc = MinimapWindowProc;
    wc.hInstance = g_hModule;
    wc.lpszClassName = MINIMAP_WINDOW_CLASS;
    RegisterClassW(&wc);  // Ignore error if already registered

    // Create the minimap window
    HWND minimapHwnd = CreateWindowExW(0, MINIMAP_WINDOW_CLASS, L"", WS_CHILD | WS_VISIBLE,
                                       0, 0, MinimapOverlay::GetWidth(), 0, parentHwnd, NULL, g_hModule, NULL);

    if (minimapHwnd) {
        // Store Scintilla HWND in minimap window's USERDATA
        SetWindowLongPtr(minimapHwnd, GWLP_USERDATA, (LONG_PTR)scintillaHwnd);

        // Store minimap HWND as property on Scintilla window
        SetPropW(scintillaHwnd, MINIMAP_WINDOW_PROP, minimapHwnd);

        char debugMsg[256];
        sprintf_s(debugMsg, "Created minimap window: 0x%p for Scintilla: 0x%p\n", minimapHwnd, scintillaHwnd);
        OutputDebugStringA(debugMsg);
    }

    return minimapHwnd;
}

// Removed LayoutMinimapAndEditor and RestoreEditorFullWidth
// Now using LayoutMinimapIfEnabled from HookManager for all layout operations
// This ensures we use the same code path that works during window resize

void MinimapManager::EnableMinimap(HWND scintillaHwnd, HWND callbackWindow)
{
    if (!scintillaHwnd || !IsWindow(scintillaHwnd)) {
        OutputDebugStringA("EnableMinimap: Invalid Scintilla window\n");
        return;
    }

    // Check if already enabled
    if (IsMinimapEnabled(scintillaHwnd)) {
        OutputDebugStringA("EnableMinimap: Minimap already enabled\n");
        return;
    }

    HWND parentHwnd = GetParent(scintillaHwnd);
    if (!parentHwnd || !IsWindow(parentHwnd)) {
        OutputDebugStringA("EnableMinimap: Invalid parent window\n");
        return;
    }

    // Create minimap window
    HWND minimapHwnd = CreateMinimapWindow(parentHwnd, scintillaHwnd);
    if (!minimapHwnd) {
        OutputDebugStringA("EnableMinimap: Failed to create minimap window\n");
        return;
    }

    // Mark as enabled BEFORE layout so LayoutMinimapIfEnabled will see it
    SetPropW(scintillaHwnd, MINIMAP_ENABLED_PROP, (HANDLE)1);

    // Use the same layout function that handles window resize
    // This ensures consistent behavior
    LayoutMinimapIfEnabled(parentHwnd);

    char debugMsg[256];
    sprintf_s(debugMsg, "Enabled minimap for Scintilla: 0x%p\n", scintillaHwnd);
    OutputDebugStringA(debugMsg);
}

void MinimapManager::DisableMinimap(HWND scintillaHwnd)
{
    if (!scintillaHwnd || !IsWindow(scintillaHwnd)) {
        OutputDebugStringA("DisableMinimap: Invalid Scintilla window\n");
        return;
    }

    // Check if already disabled
    if (!IsMinimapEnabled(scintillaHwnd)) {
        OutputDebugStringA("DisableMinimap: Minimap already disabled\n");
        return;
    }

    HWND parentHwnd = GetParent(scintillaHwnd);
    if (!parentHwnd || !IsWindow(parentHwnd)) {
        OutputDebugStringA("DisableMinimap: Invalid parent window\n");
        return;
    }

    // Get minimap window
    HWND minimapHwnd = GetMinimapWindow(scintillaHwnd);
    if (minimapHwnd && IsWindow(minimapHwnd)) {
        // Clean up minimap drag state if this minimap was being dragged
        if (g_minimapDragHwnd == minimapHwnd) {
            g_minimapDragHwnd = NULL;
            g_isMinimapDragging = false;
            ReleaseCapture();
        }

        // Destroy minimap window
        DestroyWindow(minimapHwnd);

        char debugMsg[256];
        sprintf_s(debugMsg, "Destroyed minimap window: 0x%p\n", minimapHwnd);
        OutputDebugStringA(debugMsg);
    }

    // Remove properties BEFORE layout so LayoutMinimapIfEnabled will see it's disabled
    RemovePropW(scintillaHwnd, MINIMAP_WINDOW_PROP);
    RemovePropW(scintillaHwnd, MINIMAP_ENABLED_PROP);

    // Use the same layout function that handles window resize
    // This will restore editor to full width
    LayoutMinimapIfEnabled(parentHwnd);

    char debugMsg[256];
    sprintf_s(debugMsg, "Disabled minimap for Scintilla: 0x%p\n", scintillaHwnd);
    OutputDebugStringA(debugMsg);
}

bool MinimapManager::ToggleMinimap(HWND scintillaHwnd, HWND callbackWindow)
{
    if (!scintillaHwnd || !IsWindow(scintillaHwnd)) {
        OutputDebugStringA("ToggleMinimap: Invalid Scintilla window\n");
        return false;
    }

    bool isEnabled = IsMinimapEnabled(scintillaHwnd);

    if (isEnabled) {
        DisableMinimap(scintillaHwnd);
        return false;
    } else {
        EnableMinimap(scintillaHwnd, callbackWindow);
        return true;
    }
}
