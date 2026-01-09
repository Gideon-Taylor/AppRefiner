#pragma once

#include <Windows.h>

class MinimapOverlay
{
public:
    // Get the width of the minimap overlay
    static int GetWidth();

    // Handle WM_PAINT message to render the minimap
    static LRESULT HandlePaint(HWND minimapHwnd, HWND scintillaHwnd, WPARAM wParam, LPARAM lParam);
    static LRESULT HandlePaint(HWND hWnd, WPARAM wParam, LPARAM lParam);

    // Handle WM_LBUTTONDOWN message for click-to-scroll
    static LRESULT HandleLeftButtonDown(HWND minimapHwnd, HWND scintillaHwnd, WPARAM wParam, LPARAM lParam);
    static LRESULT HandleLeftButtonDown(HWND hWnd, WPARAM wParam, LPARAM lParam);

    // Handle WM_MOUSEMOVE message to show/hide viewport when hovering
    static LRESULT HandleMouseMove(HWND hWnd, WPARAM wParam, LPARAM lParam);

    // Handle WM_MOUSELEAVE message to hide viewport when leaving minimap
    static LRESULT HandleMouseLeave(HWND hWnd, WPARAM wParam, LPARAM lParam);

    // Clear cached minimap style data
    static void InvalidateCache();
};
