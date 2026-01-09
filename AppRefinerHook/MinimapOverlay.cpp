#include "MinimapOverlay.h"
#include "Scintilla.h"

// Minimap width in pixels
static const int MINIMAP_WIDTH = 120;

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

                // Draw viewport indicator (amber/yellow box) with transparency
                if (totalLines > 0) {
                    // Calculate viewport position and height as ratio of total document
                    float startRatio = (float)firstVisibleLine / (float)totalLines;
                    float heightRatio = (float)visibleLines / (float)totalLines;

                    // Clamp height ratio to max 1.0 (can't be larger than document)
                    if (heightRatio > 1.0f) heightRatio = 1.0f;

                    int viewportY = (int)(startRatio * windowHeight);
                    int viewportHeight = (int)(heightRatio * windowHeight);

                    // Ensure minimum height of 10px for visibility
                    if (viewportHeight < 10) viewportHeight = 10;

                    // Create another memory DC for transparent amber box
                    HDC amberDC = CreateCompatibleDC(hdc);
                    if (amberDC) {
                        HBITMAP amberBitmap = CreateCompatibleBitmap(hdc, MINIMAP_WIDTH, viewportHeight);
                        if (amberBitmap) {
                            HBITMAP oldAmberBitmap = (HBITMAP)SelectObject(amberDC, amberBitmap);

                            // Fill amber DC with amber color
                            HBRUSH amberBrush = CreateSolidBrush(RGB(255, 191, 0));
                            RECT amberRect = { 0, 0, MINIMAP_WIDTH, viewportHeight };
                            FillRect(amberDC, &amberRect, amberBrush);
                            DeleteObject(amberBrush);

                            // Blend amber box onto memory DC with 50% opacity
                            BLENDFUNCTION blend = { 0 };
                            blend.BlendOp = AC_SRC_OVER;
                            blend.BlendFlags = 0;
                            blend.SourceConstantAlpha = 128; // 50% opacity
                            blend.AlphaFormat = 0;

                            AlphaBlend(memDC, 0, viewportY, MINIMAP_WIDTH, viewportHeight,
                                       amberDC, 0, 0, MINIMAP_WIDTH, viewportHeight, blend);

                            // Cleanup amber DC
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

        // Calculate click position as percentage of minimap height
        float clickRatio = (float)yPos / (float)windowHeight;

        // Convert to middle line number (where user wants to center viewport)
        int middleLine = (int)(clickRatio * totalLines);

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
