#include "MinimapOverlay.h"
#include "Scintilla.h"

// Minimap width in pixels
static const int MINIMAP_WIDTH = 120;
// Minimum viewport height for visibility.
static const int MINIMAP_MIN_VIEWPORT_HEIGHT = 20;
// Keep minimap from representing too many lines at once vs the viewport.
static const int MINIMAP_MAX_VIEWPORTS = 12;

static bool g_isMinimapHover = false;
static bool g_isTrackingMouseLeave = false;

static RECT GetMinimapRect(HWND hWnd)
{
    RECT clientRect;
    GetClientRect(hWnd, &clientRect);
    RECT minimapRect = { 0 };
    minimapRect.left = clientRect.right - MINIMAP_WIDTH;
    minimapRect.right = clientRect.right;
    minimapRect.top = 0;
    minimapRect.bottom = clientRect.bottom;
    return minimapRect;
}

static bool IsPointInRect(const RECT& rect, int x, int y)
{
    return x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom;
}

static int GetLineHeight(HWND hWnd)
{
    LRESULT lineHeight = SendMessage(hWnd, SCI_TEXTHEIGHT, 0, 0);
    if (lineHeight < 1) lineHeight = 1;
    return (int)lineHeight;
}

static float GetMinimapLineHeight(int lineHeight)
{
    float minimapLineHeight = (float)lineHeight / 10.0f;
    if (minimapLineHeight < 1.0f) minimapLineHeight = 1.0f;
    return minimapLineHeight;
}

static int GetEffectiveTotalLines(int totalLines, int windowHeight, int lineHeight, int visibleLines)
{
    if (totalLines < 1) return 0;
    if (windowHeight < 1) return totalLines;

    float minimapLineHeight = GetMinimapLineHeight(lineHeight);
    int maxLinesByMinimap = (int)((float)windowHeight / minimapLineHeight);
    int maxLinesByViewport = (visibleLines > 0) ? (visibleLines * MINIMAP_MAX_VIEWPORTS) : maxLinesByMinimap;
    int maxLinesRepresented = (maxLinesByMinimap < maxLinesByViewport) ? maxLinesByMinimap : maxLinesByViewport;
    if (maxLinesRepresented < 1) maxLinesRepresented = 1;
    return (totalLines > maxLinesRepresented) ? maxLinesRepresented : totalLines;
}

static int GetMinimapWindowStart(int totalLines, int visibleLines, int effectiveTotalLines, int firstVisibleLine)
{
    if (totalLines < 1 || effectiveTotalLines < 1) return 0;

    int centerLine = firstVisibleLine + (visibleLines / 2);
    if (centerLine < 0) centerLine = 0;
    if (centerLine > totalLines) centerLine = totalLines;

    float docRatio = (float)centerLine / (float)totalLines;
    if (docRatio < 0.0f) docRatio = 0.0f;
    if (docRatio > 1.0f) docRatio = 1.0f;

    int desiredCenterOffset = (int)(docRatio * (float)effectiveTotalLines);
    int windowStart = centerLine - desiredCenterOffset;
    int maxWindowStart = totalLines - effectiveTotalLines;
    if (maxWindowStart < 0) maxWindowStart = 0;
    if (windowStart < 0) windowStart = 0;
    if (windowStart > maxWindowStart) windowStart = maxWindowStart;

    return windowStart;
}

int MinimapOverlay::GetWidth()
{
    return MINIMAP_WIDTH;
}

LRESULT MinimapOverlay::HandleEraseBkgnd(HWND hWnd, WPARAM wParam, LPARAM lParam)
{
    // Get window dimensions to calculate minimap area
    RECT clientRect;
    GetClientRect(hWnd, &clientRect);
    int windowWidth = clientRect.right;
    int minimapX = windowWidth - MINIMAP_WIDTH;

    // Get the region being erased
    HDC hdc = (HDC)wParam;
    RECT eraseRect;
    GetClipBox(hdc, &eraseRect);

    // Check if erase region overlaps with minimap area
    if (eraseRect.right > minimapX) {
        // Erase only the non-minimap area (left side)
        if (eraseRect.left < minimapX) {
            RECT leftRect = eraseRect;
            leftRect.right = minimapX;
            HBRUSH bgBrush = (HBRUSH)GetClassLongPtr(hWnd, GCLP_HBRBACKGROUND);
            if (bgBrush) {
                FillRect(hdc, &leftRect, bgBrush);
            }
        }
        // Don't erase the minimap area - return 1 to indicate we handled it
        return 1;
    }

    // Fall through to default for non-minimap areas
    return 0;
}

LRESULT MinimapOverlay::HandlePaint(HWND hWnd, WPARAM wParam, LPARAM lParam)
{
    // Get window dimensions
    RECT clientRect;
    GetClientRect(hWnd, &clientRect);
    int windowWidth = clientRect.right;
    int windowHeight = clientRect.bottom;

    // Minimap dimensions: full height, on right side
    int minimapX = windowWidth - MINIMAP_WIDTH;
    int minimapY = 0;

    // Get Scintilla document metrics for viewport indicator
    LRESULT totalLines = SendMessage(hWnd, SCI_GETLINECOUNT, 0, 0);
    LRESULT firstVisibleLine = SendMessage(hWnd, SCI_GETFIRSTVISIBLELINE, 0, 0);
    LRESULT visibleLines = SendMessage(hWnd, SCI_LINESONSCREEN, 0, 0);
    int lineHeight = GetLineHeight(hWnd);

    HDC hdc = GetDC(hWnd);
    if (hdc) {
        // Double buffering: create memory DC for the entire minimap
        HDC memDC = CreateCompatibleDC(hdc);
        if (memDC) {
            HBITMAP memBitmap = CreateCompatibleBitmap(hdc, MINIMAP_WIDTH, windowHeight);
            if (memBitmap) {
                HBITMAP oldBitmap = (HBITMAP)SelectObject(memDC, memBitmap);

                // Draw opaque white background to memory DC
                HBRUSH whiteBrush = CreateSolidBrush(RGB(255, 255, 255));
                RECT minimapRect = { 0, 0, MINIMAP_WIDTH, windowHeight };
                FillRect(memDC, &minimapRect, whiteBrush);
                DeleteObject(whiteBrush);

                // Draw viewport indicator only when hovering over the minimap
                if (totalLines > 0 && g_isMinimapHover) {
                    // Position remains relative to full document; height respects minimum zoom.
                    int effectiveTotalLines = GetEffectiveTotalLines((int)totalLines, windowHeight, lineHeight, (int)visibleLines);
                    if (effectiveTotalLines < 1) effectiveTotalLines = 1;

                    int windowStart = GetMinimapWindowStart((int)totalLines, (int)visibleLines, effectiveTotalLines, (int)firstVisibleLine);
                    int windowOffset = (int)firstVisibleLine - windowStart;
                    if (windowOffset < 0) windowOffset = 0;
                    if (windowOffset > effectiveTotalLines) windowOffset = effectiveTotalLines;

                    float startRatio = (float)windowOffset / (float)effectiveTotalLines;
                    float heightRatio = (float)visibleLines / (float)effectiveTotalLines;

                    // Clamp height ratio to max 1.0 (can't be larger than document)
                    if (heightRatio > 1.0f) heightRatio = 1.0f;

                    int viewportY = (int)(startRatio * windowHeight);
                    int viewportHeight = (int)(heightRatio * windowHeight);

                    // Ensure minimum height for visibility
                    if (viewportHeight < MINIMAP_MIN_VIEWPORT_HEIGHT) viewportHeight = MINIMAP_MIN_VIEWPORT_HEIGHT;
                    if (viewportHeight > windowHeight) viewportHeight = windowHeight;
                    if (viewportY < 0) viewportY = 0;
                    if (viewportY + viewportHeight > windowHeight) {
                        viewportY = windowHeight - viewportHeight;
                    }

                    // Create another memory DC for transparent viewport box
                    HDC amberDC = CreateCompatibleDC(hdc);
                    if (amberDC) {
                        HBITMAP amberBitmap = CreateCompatibleBitmap(hdc, MINIMAP_WIDTH, viewportHeight);
                        if (amberBitmap) {
                            HBITMAP oldAmberBitmap = (HBITMAP)SelectObject(amberDC, amberBitmap);

                            // Fill viewport DC with gray color
                            HBRUSH amberBrush = CreateSolidBrush(RGB(201, 201, 201));
                            RECT amberRect = { 0, 0, MINIMAP_WIDTH, viewportHeight };
                            FillRect(amberDC, &amberRect, amberBrush);
                            DeleteObject(amberBrush);

                            // Blend viewport box onto memory DC with 50% opacity
                            BLENDFUNCTION blend = { 0 };
                            blend.BlendOp = AC_SRC_OVER;
                            blend.BlendFlags = 0;
                            blend.SourceConstantAlpha = 128; // 50% opacity
                            blend.AlphaFormat = 0;

                            AlphaBlend(memDC, 0, viewportY, MINIMAP_WIDTH, viewportHeight,
                                       amberDC, 0, 0, MINIMAP_WIDTH, viewportHeight, blend);

                            // Cleanup viewport DC
                            SelectObject(amberDC, oldAmberBitmap);
                            DeleteObject(amberBitmap);
                        }
                        DeleteDC(amberDC);
                    }
                }

                // Draw thin black border on memory DC
                HPEN blackPen = CreatePen(PS_SOLID, 1, RGB(0, 0, 0));
                HPEN oldPen = (HPEN)SelectObject(memDC, blackPen);
                HBRUSH oldBrush = (HBRUSH)SelectObject(memDC, GetStockObject(NULL_BRUSH));

                Rectangle(memDC, 0, 0, MINIMAP_WIDTH, windowHeight);

                SelectObject(memDC, oldPen);
                SelectObject(memDC, oldBrush);
                DeleteObject(blackPen);

                // BitBlt the entire minimap to screen in one operation (eliminates flicker)
                BitBlt(hdc, minimapX, minimapY, MINIMAP_WIDTH, windowHeight,
                       memDC, 0, 0, SRCCOPY);

                // Cleanup
                SelectObject(memDC, oldBitmap);
                DeleteObject(memBitmap);
            }
            DeleteDC(memDC);
        }

        ReleaseDC(hWnd, hdc);
    }

    return 0;
}

LRESULT MinimapOverlay::HandleLeftButtonDown(HWND hWnd, WPARAM wParam, LPARAM lParam)
{
    // Extract mouse coordinates from lParam
    int xPos = LOWORD(lParam);
    int yPos = HIWORD(lParam);

    // Get window dimensions to calculate minimap bounds
    RECT clientRect;
    GetClientRect(hWnd, &clientRect);
    int windowWidth = clientRect.right;
    int windowHeight = clientRect.bottom;
    int minimapX = windowWidth - MINIMAP_WIDTH;

    // Check if click is inside the minimap overlay
    if (xPos >= minimapX && xPos <= windowWidth) {
        // Get Scintilla document metrics
        LRESULT totalLines = SendMessage(hWnd, SCI_GETLINECOUNT, 0, 0);
        LRESULT visibleLines = SendMessage(hWnd, SCI_LINESONSCREEN, 0, 0);
        LRESULT firstVisibleLine = SendMessage(hWnd, SCI_GETFIRSTVISIBLELINE, 0, 0);
        int lineHeight = GetLineHeight(hWnd);

        if (totalLines <= 0) {
            return 0;
        }

        int effectiveTotalLines = GetEffectiveTotalLines((int)totalLines, windowHeight, lineHeight, (int)visibleLines);
        if (effectiveTotalLines < 1) effectiveTotalLines = 1;

        int windowStart = GetMinimapWindowStart((int)totalLines, (int)visibleLines, effectiveTotalLines, (int)firstVisibleLine);

        // Calculate click position as percentage of minimap height
        float clickRatio = (float)yPos / (float)windowHeight;
        if (clickRatio < 0.0f) clickRatio = 0.0f;
        if (clickRatio > 1.0f) clickRatio = 1.0f;

        // Convert to middle line number (where user wants to center viewport)
        int middleLine = windowStart + (int)(clickRatio * (float)effectiveTotalLines);

        // Calculate target first visible line (center viewport on clicked position)
        int targetFirstVisible = middleLine - (visibleLines / 2);

        // Clamp to valid range [0, totalLines - visibleLines]
        if (targetFirstVisible < 0) targetFirstVisible = 0;
        int maxFirstVisible = (int)(totalLines - visibleLines);
        if (maxFirstVisible < 0) maxFirstVisible = 0;
        if (targetFirstVisible > maxFirstVisible) targetFirstVisible = maxFirstVisible;

        // Scroll to the calculated line
        SendMessage(hWnd, SCI_SETFIRSTVISIBLELINE, targetFirstVisible, 0);

        // Consume the click event (don't pass to Scintilla)
        return 0;
    }

    // Click outside minimap - let Scintilla handle it
    return -1; // Signal to continue with default processing
}

LRESULT MinimapOverlay::HandleMouseMove(HWND hWnd, WPARAM wParam, LPARAM lParam)
{
    int xPos = LOWORD(lParam);
    int yPos = HIWORD(lParam);

    RECT minimapRect = GetMinimapRect(hWnd);
    bool isHovering = IsPointInRect(minimapRect, xPos, yPos);
    if (isHovering && !g_isTrackingMouseLeave) {
        TRACKMOUSEEVENT tme = { 0 };
        tme.cbSize = sizeof(TRACKMOUSEEVENT);
        tme.dwFlags = TME_LEAVE;
        tme.hwndTrack = hWnd;
        if (TrackMouseEvent(&tme)) {
            g_isTrackingMouseLeave = true;
        }
    }

    if (isHovering != g_isMinimapHover) {
        g_isMinimapHover = isHovering;
        InvalidateRect(hWnd, &minimapRect, FALSE);
    }

    return -1;
}

LRESULT MinimapOverlay::HandleMouseLeave(HWND hWnd, WPARAM wParam, LPARAM lParam)
{
    if (g_isMinimapHover) {
        g_isMinimapHover = false;
        RECT minimapRect = GetMinimapRect(hWnd);
        InvalidateRect(hWnd, &minimapRect, FALSE);
    }
    g_isTrackingMouseLeave = false;
    return -1;
}
