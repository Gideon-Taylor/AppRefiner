#include "MinimapOverlay.h"
#include "Scintilla.h"
#include <vector>

// Minimap width in pixels
static const int MINIMAP_WIDTH = 120;
// Minimum viewport height for visibility.
static const int MINIMAP_MIN_VIEWPORT_HEIGHT = 20;
// Keep minimap from representing too many lines at once vs the viewport.
static const int MINIMAP_MAX_VIEWPORTS = 12;
// Scale factors for minimap content rendering.
static const int MINIMAP_MAX_CHARS_FOR_FULL = 200;
static const int MINIMAP_INDENT_TAB_WIDTH = 4;
// Style IDs for the VB/PB lexer.
static const int SCE_B_COMMENT = 1;
static const int SCE_B_KEYWORD = 3;
static const int SCE_B_STRING = 4;

static bool g_isMinimapHover = false;
static bool g_isTrackingMouseLeave = false;
static bool g_hasMinimapWindowStart = false;
static int g_cachedMinimapWindowStart = 0;

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
    if (minimapLineHeight < 2.0f) minimapLineHeight = 2.0f;
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

static int GetStableMinimapWindowStart(int totalLines, int visibleLines, int effectiveTotalLines, int firstVisibleLine)
{
    int desiredWindowStart = GetMinimapWindowStart(totalLines, visibleLines, effectiveTotalLines, firstVisibleLine);
    if (!g_hasMinimapWindowStart) {
        g_cachedMinimapWindowStart = desiredWindowStart;
        g_hasMinimapWindowStart = true;
        return g_cachedMinimapWindowStart;
    }

    int maxWindowStart = totalLines - effectiveTotalLines;
    if (maxWindowStart < 0) maxWindowStart = 0;

    if (g_cachedMinimapWindowStart > maxWindowStart) {
        g_cachedMinimapWindowStart = maxWindowStart;
    }

    int centerLine = firstVisibleLine + (visibleLines / 2);
    int margin = effectiveTotalLines / 4;
    if (margin < visibleLines) margin = visibleLines;
    if (margin < 1) margin = 1;

    int windowTop = g_cachedMinimapWindowStart + margin;
    int windowBottom = g_cachedMinimapWindowStart + effectiveTotalLines - margin;
    if (windowBottom < windowTop) windowBottom = windowTop;

    if (centerLine < windowTop) {
        g_cachedMinimapWindowStart = centerLine - margin;
    } else if (centerLine > windowBottom) {
        g_cachedMinimapWindowStart = centerLine - (effectiveTotalLines - margin);
    }

    if (g_cachedMinimapWindowStart < 0) g_cachedMinimapWindowStart = 0;
    if (g_cachedMinimapWindowStart > maxWindowStart) g_cachedMinimapWindowStart = maxWindowStart;

    return g_cachedMinimapWindowStart;
}

static HBRUSH GetMinimapBrushForStyle(int style, HBRUSH codeBrush, HBRUSH commentBrush, HBRUSH stringBrush, HBRUSH keywordBrush)
{
    if (style == SCE_B_COMMENT) {
        return commentBrush;
    }
    if (style == SCE_B_STRING) {
        return stringBrush;
    }
    if (style == SCE_B_KEYWORD) {
        return keywordBrush;
    }
    return codeBrush;
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

                int effectiveTotalLines = GetEffectiveTotalLines((int)totalLines, windowHeight, lineHeight, (int)visibleLines);
                if (effectiveTotalLines < 1) effectiveTotalLines = 1;
                int windowStart = GetStableMinimapWindowStart((int)totalLines, (int)visibleLines, effectiveTotalLines, (int)firstVisibleLine);

                // Draw content bars for the minimap.
                if (totalLines > 0) {
                    float minimapLineHeight = GetMinimapLineHeight(lineHeight);
                    int rowHeight = (int)minimapLineHeight;
                    if (rowHeight < 2) rowHeight = 2;

                    HBRUSH codeBrush = CreateSolidBrush(RGB(140, 140, 140));
                    HBRUSH commentBrush = CreateSolidBrush(RGB(0, 128, 0));
                    HBRUSH stringBrush = CreateSolidBrush(RGB(200, 0, 0));
                    HBRUSH keywordBrush = CreateSolidBrush(RGB(0, 0, 200));

                    for (int y = 0; y < windowHeight; y += rowHeight) {
                        float rowRatio = (float)(y + (rowHeight / 2)) / (float)windowHeight;
                        int lineOffset = (int)(rowRatio * (float)effectiveTotalLines);
                        int lineIndex = windowStart + lineOffset;
                        if (lineIndex < 0) lineIndex = 0;
                        if (lineIndex >= totalLines) break;

                        int lineStartPos = (int)SendMessage(hWnd, SCI_POSITIONFROMLINE, lineIndex, 0);
                        int lineLength = (int)SendMessage(hWnd, SCI_LINELENGTH, lineIndex, 0);
                        if (lineStartPos < 0 || lineLength <= 0) {
                            continue;
                        }

                        int indentColumns = 0;
                        int maxIndentScan = lineLength < 40 ? lineLength : 40;
                        for (int i = 0; i < maxIndentScan; i++) {
                            int ch = (int)SendMessage(hWnd, SCI_GETCHARAT, lineStartPos + i, 0);
                            if (ch == ' ') {
                                indentColumns += 1;
                            } else if (ch == '\t') {
                                indentColumns += MINIMAP_INDENT_TAB_WIDTH;
                            } else {
                                break;
                            }
                        }

                        int indentPixels = indentColumns / 2;
                        if (indentPixels > MINIMAP_WIDTH / 2) indentPixels = MINIMAP_WIDTH / 2;

                        int maxChars = lineLength > MINIMAP_MAX_CHARS_FOR_FULL ? MINIMAP_MAX_CHARS_FOR_FULL : lineLength;
                        int barMaxWidth = MINIMAP_WIDTH - 6;
                        if (barMaxWidth < 1) barMaxWidth = 1;
                        int barWidth = 2 + (barMaxWidth * maxChars / MINIMAP_MAX_CHARS_FOR_FULL);
                        if (barWidth < 2) barWidth = 2;
                        if (barWidth > barMaxWidth) barWidth = barMaxWidth;

                        int startX = 2 + indentPixels;
                        if (startX > MINIMAP_WIDTH - 2) startX = MINIMAP_WIDTH - 2;
                        int availableWidth = MINIMAP_WIDTH - 2 - startX;
                        if (availableWidth < 1) {
                            continue;
                        }
                        if (barWidth > availableWidth) barWidth = availableWidth;

                        int rangeStart = lineStartPos;
                        int rangeEnd = lineStartPos + maxChars;
                        Sci_TextRange tr;
                        tr.chrg.cpMin = rangeStart;
                        tr.chrg.cpMax = rangeEnd;
                        int bufferSize = (maxChars * 2) + 2;
                        std::vector<char> styledText(bufferSize, 0);
                        tr.lpstrText = styledText.data();
                        SendMessage(hWnd, SCI_GETSTYLEDTEXT, 0, (LPARAM)&tr);

                        bool hasNonWhitespace = false;
                        for (int i = 0; i < maxChars; i++) {
                            int ch = (unsigned char)styledText[i * 2];
                            if (ch == 0) {
                                break;
                            }
                            if (ch != ' ' && ch != '\t' && ch != '\r' && ch != '\n') {
                                hasNonWhitespace = true;
                                break;
                            }
                        }
                        if (!hasNonWhitespace) {
                            continue;
                        }

                        int runStyle = -1;
                        int runLength = 0;
                        float xCursor = (float)startX;
                        float widthPerChar = (maxChars > 0) ? ((float)barWidth / (float)maxChars) : 0.0f;
                        int endX = startX + barWidth;
                        bool isFirstRun = true;

                        for (int i = 0; i < maxChars; i++) {
                            int ch = (unsigned char)styledText[i * 2];
                            int style = (unsigned char)styledText[(i * 2) + 1];
                            if (ch == 0) {
                                break;
                            }

                            if (runStyle == -1) {
                                runStyle = style;
                                runLength = 1;
                            } else if (style == runStyle) {
                                runLength++;
                            } else {
                                int segmentWidth = (int)((float)runLength * widthPerChar + 0.5f);
                                if (segmentWidth < 1) segmentWidth = 1;
                                if (!isFirstRun && xCursor < endX) {
                                    xCursor += 1.0f;
                                }
                                int drawWidth = (int)segmentWidth;
                                if ((int)xCursor + drawWidth > endX) {
                                    drawWidth = endX - (int)xCursor;
                                }
                                if (drawWidth > 0) {
                                    RECT lineRect = { (int)xCursor, y, (int)xCursor + drawWidth, y + 1 };
                                    FillRect(memDC, &lineRect, GetMinimapBrushForStyle(runStyle, codeBrush, commentBrush, stringBrush, keywordBrush));
                                    xCursor += (float)drawWidth;
                                }

                                runStyle = style;
                                runLength = 1;
                                isFirstRun = false;
                            }
                        }

                        if (runStyle != -1 && runLength > 0) {
                            int segmentWidth = (int)((float)runLength * widthPerChar + 0.5f);
                            if (segmentWidth < 1) segmentWidth = 1;
                            if (!isFirstRun && xCursor < endX) {
                                xCursor += 1.0f;
                            }
                            int drawWidth = (int)segmentWidth;
                            if ((int)xCursor + drawWidth > endX) {
                                drawWidth = endX - (int)xCursor;
                            }
                            if (drawWidth > 0) {
                                RECT lineRect = { (int)xCursor, y, (int)xCursor + drawWidth, y + 1 };
                                FillRect(memDC, &lineRect, GetMinimapBrushForStyle(runStyle, codeBrush, commentBrush, stringBrush, keywordBrush));
                            }
                        }
                    }

                    DeleteObject(codeBrush);
                    DeleteObject(commentBrush);
                    DeleteObject(stringBrush);
                    DeleteObject(keywordBrush);
                }

                // Draw viewport indicator only when hovering over the minimap
                if (totalLines > 0 && g_isMinimapHover) {
                    // Position remains relative to full document; height respects minimum zoom.
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

        int windowStart = GetStableMinimapWindowStart((int)totalLines, (int)visibleLines, effectiveTotalLines, (int)firstVisibleLine);

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
