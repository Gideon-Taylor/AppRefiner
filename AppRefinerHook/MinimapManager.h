#pragma once

#include <Windows.h>

// Minimap management functionality
// Handles enabling, disabling, and toggling the minimap for Scintilla editors
class MinimapManager
{
public:
    // Enable the minimap for a given Scintilla editor
    // Creates the minimap window and resizes the editor to make room
    static void EnableMinimap(HWND scintillaHwnd, HWND callbackWindow);

    // Disable the minimap for a given Scintilla editor
    // Destroys the minimap window and restores the editor to full width
    static void DisableMinimap(HWND scintillaHwnd);

    // Toggle the minimap on/off for a given Scintilla editor
    // Returns true if minimap is now enabled, false if disabled
    static bool ToggleMinimap(HWND scintillaHwnd, HWND callbackWindow);

    // Check if the minimap is currently enabled for a given Scintilla editor
    static bool IsMinimapEnabled(HWND scintillaHwnd);

    // Get the minimap window handle for a given Scintilla editor (if exists)
    static HWND GetMinimapWindow(HWND scintillaHwnd);

private:
    // Create the minimap window for a given Scintilla editor
    static HWND CreateMinimapWindow(HWND parentHwnd, HWND scintillaHwnd);

    // Note: Layout is now handled by LayoutMinimapIfEnabled() in HookManager
    // to ensure consistent behavior with window resize
};
