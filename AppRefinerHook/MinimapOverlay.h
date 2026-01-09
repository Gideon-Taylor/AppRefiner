#pragma once

#include <Windows.h>

class MinimapOverlay
{
public:
    // Get the width of the minimap overlay
    static int GetWidth();

    // Handle WM_ERASEBKGND message to prevent flicker
    static LRESULT HandleEraseBkgnd(HWND hWnd, WPARAM wParam, LPARAM lParam);

    // Handle WM_PAINT message to render the minimap
    static LRESULT HandlePaint(HWND hWnd, WPARAM wParam, LPARAM lParam);

    // Handle WM_LBUTTONDOWN message for click-to-scroll
    static LRESULT HandleLeftButtonDown(HWND hWnd, WPARAM wParam, LPARAM lParam);

    // Handle WM_MOUSEMOVE message to show/hide viewport when hovering
    static LRESULT HandleMouseMove(HWND hWnd, WPARAM wParam, LPARAM lParam);

    // Handle WM_MOUSELEAVE message to hide viewport when leaving minimap
    static LRESULT HandleMouseLeave(HWND hWnd, WPARAM wParam, LPARAM lParam);
};
