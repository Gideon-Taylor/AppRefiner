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
static const int MINIMAP_NATURAL_LINE_SPACING = 3;
static const int MINIMAP_NATURAL_LINE_HEIGHT = 2;
// Style IDs for the PeopleCode lexer.
static const int SCE_B_COMMENT = 23;
static const int SCE_B_KEYWORD = 3;
static const int SCE_B_STRING = 4;
static const BYTE MINIMAP_INDICATOR_ALPHA = 64;

static bool g_isMinimapHover = false;
static bool g_isTrackingMouseLeave = false;
static bool g_hasMinimapWindowStart = false;
static int g_cachedMinimapWindowStart = 0;
static bool g_hasForcedColorise = false;
static HWND g_lastColoriseHwnd = NULL;
static HWND g_cacheHwnd = NULL;
static int g_cacheGeneration = 1;

struct LineStyleCache {
    int generation = 0;
    int lineLength = 0;
    int maxChars = 0;
    std::vector<char> styledText;
};

static std::vector<LineStyleCache> g_lineStyleCache;

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

static void EnsureCacheForHwnd(HWND hWnd, int lineCount)
{
    if (g_cacheHwnd != hWnd) {
        g_cacheHwnd = hWnd;
        g_lineStyleCache.clear();
        g_cacheGeneration++;
    }

    if (lineCount > 0 && (int)g_lineStyleCache.size() < lineCount) {
        g_lineStyleCache.resize(lineCount);
    }
}

static const std::vector<char>& GetStyledTextCached(HWND hWnd, int lineIndex, int lineStartPos, int lineLength, int maxChars)
{
    EnsureCacheForHwnd(hWnd, lineIndex + 1);
    LineStyleCache& entry = g_lineStyleCache[lineIndex];
    if (entry.generation == g_cacheGeneration && entry.lineLength == lineLength && entry.maxChars == maxChars) {
        return entry.styledText;
    }

    entry.generation = g_cacheGeneration;
    entry.lineLength = lineLength;
    entry.maxChars = maxChars;
    entry.styledText.assign((maxChars * 2) + 2, 0);

    Sci_TextRange tr;
    tr.chrg.cpMin = lineStartPos;
    tr.chrg.cpMax = lineStartPos + maxChars;
    tr.lpstrText = entry.styledText.data();
    SendMessage(hWnd, SCI_GETSTYLEDTEXT, 0, (LPARAM)&tr);

    return entry.styledText;
}

static COLORREF GetMinimapColorForStyle(int style, COLORREF codeColor, COLORREF commentColor, COLORREF stringColor, COLORREF keywordColor)
{
    if (style == SCE_B_COMMENT) {
        return commentColor;
    }
    if (style == SCE_B_STRING) {
        return stringColor;
    }
    if (style == SCE_B_KEYWORD) {
        return keywordColor;
    }
    return codeColor;
}

static bool TryGetLineIndicator(HWND hWnd, int lineStartPos, int lineLength, COLORREF& indicatorColor)
{
    for (int i = 0; i < lineLength; i++) {
        int pos = lineStartPos + i;
        int mask = (int)SendMessage(hWnd, SCI_INDICATORALLONFOR, pos, 0);
        if (mask == 0) {
            continue;
        }

        int indicatorIndex = 0;
        while ((mask & 1) == 0 && indicatorIndex < INDIC_MAX) {
            mask >>= 1;
            indicatorIndex++;
        }

        if (indicatorIndex <= INDIC_MAX) {
            indicatorColor = (COLORREF)SendMessage(hWnd, SCI_INDICGETFORE, indicatorIndex, 0);
            return true;
        }
    }

    return false;
}

int MinimapOverlay::GetWidth()
{
    return MINIMAP_WIDTH;
}

void MinimapOverlay::InvalidateCache()
{
    g_cacheGeneration++;
}

LRESULT MinimapOverlay::HandlePaint(HWND minimapHwnd, HWND scintillaHwnd, WPARAM wParam, LPARAM lParam)
{
    if (!minimapHwnd || !scintillaHwnd || !IsWindow(minimapHwnd) || !IsWindow(scintillaHwnd)) {
        return 0;
    }

    // Get window dimensions
    RECT clientRect;
    GetClientRect(minimapHwnd, &clientRect);
    int windowWidth = clientRect.right;
    int windowHeight = clientRect.bottom;

    // Minimap dimensions: full height, full width
    int minimapX = 0;
    int minimapY = 0;

    // Get Scintilla document metrics for viewport indicator
    LRESULT totalLines = SendMessage(scintillaHwnd, SCI_GETLINECOUNT, 0, 0);
    LRESULT firstVisibleLine = SendMessage(scintillaHwnd, SCI_GETFIRSTVISIBLELINE, 0, 0);
    LRESULT visibleLines = SendMessage(scintillaHwnd, SCI_LINESONSCREEN, 0, 0);
    int lineHeight = GetLineHeight(scintillaHwnd);

    if (g_lastColoriseHwnd != scintillaHwnd) {
        g_lastColoriseHwnd = scintillaHwnd;
        g_hasForcedColorise = false;
    }

    if (!g_hasForcedColorise) {
        SendMessage(scintillaHwnd, SCI_COLOURISE, 0, -1);
        g_hasForcedColorise = true;
    }

    PAINTSTRUCT ps;
    HDC hdc = BeginPaint(minimapHwnd, &ps);
    if (hdc) {
        // Double buffering: create memory DC for the entire minimap
        HDC memDC = CreateCompatibleDC(hdc);
        if (memDC) {
            HBITMAP memBitmap = CreateCompatibleBitmap(hdc, windowWidth, windowHeight);
            if (memBitmap) {
                HBITMAP oldBitmap = (HBITMAP)SelectObject(memDC, memBitmap);

                // Draw opaque white background to memory DC
                HBRUSH whiteBrush = CreateSolidBrush(RGB(255, 255, 255));
                RECT minimapRect = { 0, 0, windowWidth, windowHeight };
                FillRect(memDC, &minimapRect, whiteBrush);
                DeleteObject(whiteBrush);

                int effectiveTotalLines = GetEffectiveTotalLines((int)totalLines, windowHeight, lineHeight, (int)visibleLines);
                if (effectiveTotalLines < 1) effectiveTotalLines = 1;
                int windowStart = GetStableMinimapWindowStart((int)totalLines, (int)visibleLines, effectiveTotalLines, (int)firstVisibleLine);

                // Draw content bars for the minimap.
                if (totalLines > 0) {
                    // Determine rendering mode based on document length
                    // Mode 1: All lines fit - render all lines with spacing >= 3px, text at 2px minimum
                    // Mode 2: Compressed - sliding window for very long documents (spacing < 3px)

                    float optimalSpacing = (float)windowHeight / (float)totalLines;
                    bool useAllLinesMode = (optimalSpacing >= 3.0f);

                    float minimapLineHeight;
                    int rowHeight;
                    int lineDrawHeight;

                    if (useAllLinesMode) {
                        // All lines fit mode: render all lines with natural spacing
                        // Scale text height based on available space, but don't stretch gaps
                        lineDrawHeight = (int)((float)optimalSpacing * 0.4f);
                        if (lineDrawHeight < MINIMAP_NATURAL_LINE_HEIGHT) {
                            lineDrawHeight = MINIMAP_NATURAL_LINE_HEIGHT;
                        }
                        if (lineDrawHeight > 8) {
                            lineDrawHeight = 8;
                        }
                        // Use fixed gap instead of stretching to fill height
                        int fixedGap = 2;
                        rowHeight = lineDrawHeight + fixedGap;
                        minimapLineHeight = (float)rowHeight;
                    } else {
                        // Compressed mode: use sliding window
                        minimapLineHeight = GetMinimapLineHeight(lineHeight);
                        rowHeight = (int)minimapLineHeight;
                        if (rowHeight < 2) rowHeight = 2;
                        lineDrawHeight = (rowHeight >= 2) ? 2 : 1;
                    }

                    // Create tiny font for text rendering
                    HFONT minimapFont = CreateFont(
                        lineDrawHeight,           // Height in pixels
                        0,                        // Width (auto)
                        0,                        // Escapement
                        0,                        // Orientation
                        FW_NORMAL,                // Weight
                        FALSE,                    // Italic
                        FALSE,                    // Underline
                        FALSE,                    // Strikeout
                        DEFAULT_CHARSET,          // Charset
                        OUT_DEFAULT_PRECIS,       // Output precision
                        CLIP_DEFAULT_PRECIS,      // Clipping precision
                        NONANTIALIASED_QUALITY,   // Quality (no antialiasing for tiny text)
                        FIXED_PITCH | FF_MODERN,  // Fixed-width font
                        TEXT("Consolas")          // Font name
                    );
                    HFONT oldFont = (HFONT)SelectObject(memDC, minimapFont);
                    SetBkMode(memDC, TRANSPARENT);

                    // Color definitions for syntax highlighting
                    COLORREF codeColor = RGB(140, 140, 140);
                    COLORREF commentColor = RGB(0, 128, 0);
                    COLORREF stringColor = RGB(250, 128, 114);
                    COLORREF keywordColor = RGB(58, 58, 255);

                    HDC overlayDC = CreateCompatibleDC(hdc);
                    HBITMAP overlayBitmap = CreateCompatibleBitmap(hdc, 1, 1);
                    HBITMAP oldOverlayBitmap = overlayBitmap ? (HBITMAP)SelectObject(overlayDC, overlayBitmap) : NULL;
                    BLENDFUNCTION overlayBlend = { 0 };
                    overlayBlend.BlendOp = AC_SRC_OVER;
                    overlayBlend.BlendFlags = 0;
                    overlayBlend.SourceConstantAlpha = MINIMAP_INDICATOR_ALPHA;
                    overlayBlend.AlphaFormat = 0;

                    int maxY = useAllLinesMode ? (totalLines * rowHeight) : windowHeight;

                    for (int y = 0; y < maxY; y += rowHeight) {
                        int lineIndex;
                        if (useAllLinesMode) {
                            // All lines mode: sequential line rendering
                            lineIndex = y / rowHeight;
                            if (lineIndex >= totalLines) break;
                        } else {
                            // Compressed: calculate from window position
                            float rowRatio = (float)(y + (rowHeight / 2)) / (float)windowHeight;
                            int lineOffset = (int)(rowRatio * (float)effectiveTotalLines);
                            lineIndex = windowStart + lineOffset;
                            if (lineIndex < 0) lineIndex = 0;
                            if (lineIndex >= totalLines) break;
                        }

                        int lineStartPos = (int)SendMessage(scintillaHwnd, SCI_POSITIONFROMLINE, lineIndex, 0);
                        int lineLength = (int)SendMessage(scintillaHwnd, SCI_LINELENGTH, lineIndex, 0);
                        if (lineStartPos < 0 || lineLength <= 0) {
                            continue;
                        }

                        int indentColumns = 0;
                        int maxIndentScan = lineLength < 40 ? lineLength : 40;
                        for (int i = 0; i < maxIndentScan; i++) {
                            int ch = (int)SendMessage(scintillaHwnd, SCI_GETCHARAT, lineStartPos + i, 0);
                            if (ch == ' ') {
                                indentColumns += 1;
                            } else if (ch == '\t') {
                                indentColumns += MINIMAP_INDENT_TAB_WIDTH;
                            } else {
                                break;
                            }
                        }

                        int indentPixels = indentColumns / 2;
                        if (indentPixels > windowWidth / 2) indentPixels = windowWidth / 2;

                        COLORREF indicatorColor = 0;
                        bool hasIndicator = TryGetLineIndicator(scintillaHwnd, lineStartPos, lineLength, indicatorColor);
                        if (hasIndicator && overlayDC && overlayBitmap) {
                            // Draw transparent background
                            SetPixel(overlayDC, 0, 0, indicatorColor);
                            AlphaBlend(memDC, 0, y, windowWidth, rowHeight, overlayDC, 0, 0, 1, 1, overlayBlend);

                            // Draw fully opaque 1px border
                            HPEN borderPen = CreatePen(PS_SOLID, 1, indicatorColor);
                            HPEN oldBorderPen = (HPEN)SelectObject(memDC, borderPen);
                            HBRUSH oldBorderBrush = (HBRUSH)SelectObject(memDC, GetStockObject(NULL_BRUSH));
                            Rectangle(memDC, 0, y, windowWidth, y + rowHeight);
                            SelectObject(memDC, oldBorderPen);
                            SelectObject(memDC, oldBorderBrush);
                            DeleteObject(borderPen);
                        }

                        int maxChars = lineLength > MINIMAP_MAX_CHARS_FOR_FULL ? MINIMAP_MAX_CHARS_FOR_FULL : lineLength;
                        int startX = 2 + indentPixels;
                        if (startX > windowWidth - 2) startX = windowWidth - 2;
                        int availableWidth = windowWidth - 2 - startX;
                        if (availableWidth < 1) {
                            continue;
                        }

                        const std::vector<char>& styledText = GetStyledTextCached(scintillaHwnd, lineIndex, lineStartPos, lineLength, maxChars);

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
                        if (!hasNonWhitespace && !hasIndicator) {
                            continue;
                        }

                        // Render text character by character with syntax highlighting
                        int xCursor = startX;
                        int currentStyle = -1;

                        for (int i = 0; i < maxChars; i++) {
                            int ch = (unsigned char)styledText[i * 2];
                            int style = (unsigned char)styledText[(i * 2) + 1];
                            if (ch == 0) {
                                break;
                            }

                            // Skip rendering whitespace characters
                            if (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n') {
                                continue;
                            }

                            // Check if we've run out of horizontal space
                            if (xCursor >= windowWidth - 2) {
                                break;
                            }

                            // Set text color based on style (only if changed)
                            if (style != currentStyle) {
                                COLORREF textColor = GetMinimapColorForStyle(style, codeColor, commentColor, stringColor, keywordColor);
                                SetTextColor(memDC, textColor);
                                currentStyle = style;
                            }

                            // Draw the character
                            char charBuffer[2] = { (char)ch, '\0' };
                            TextOutA(memDC, xCursor, y, charBuffer, 1);

                            // Get character width and advance cursor
                            SIZE charSize;
                            if (GetTextExtentPoint32A(memDC, charBuffer, 1, &charSize)) {
                                xCursor += charSize.cx;
                            } else {
                                xCursor += 1; // Fallback if measurement fails
                            }
                        }
                    }

                    // Cleanup font
                    SelectObject(memDC, oldFont);
                    DeleteObject(minimapFont);

                    if (overlayDC) {
                        if (oldOverlayBitmap) {
                            SelectObject(overlayDC, oldOverlayBitmap);
                        }
                        if (overlayBitmap) {
                            DeleteObject(overlayBitmap);
                        }
                        DeleteDC(overlayDC);
                    }
                }

                // Draw viewport indicator
                if (totalLines > 0) {
                    // Determine rendering mode (need to recalculate for viewport indicator)
                    float optimalSpacing = (float)windowHeight / (float)totalLines;
                    bool useAllLinesMode = (optimalSpacing >= 3.0f);

                    int viewportY;
                    int viewportHeight;
                    int actualRowHeight;

                    if (useAllLinesMode) {
                        // Recalculate actual row height (must match rendering logic)
                        int calcLineDrawHeight = (int)((float)optimalSpacing * 0.4f);
                        if (calcLineDrawHeight < MINIMAP_NATURAL_LINE_HEIGHT) {
                            calcLineDrawHeight = MINIMAP_NATURAL_LINE_HEIGHT;
                        }
                        if (calcLineDrawHeight > 8) {
                            calcLineDrawHeight = 8;
                        }
                        int fixedGap = 2;
                        actualRowHeight = calcLineDrawHeight + fixedGap;

                        // All lines mode: use actual row height from rendering
                        viewportY = (int)firstVisibleLine * actualRowHeight;
                        viewportHeight = (int)visibleLines * actualRowHeight;
                    } else {
                        // Compressed mode: position relative to window
                        int windowOffset = (int)firstVisibleLine - windowStart;
                        if (windowOffset < 0) windowOffset = 0;
                        if (windowOffset > effectiveTotalLines) windowOffset = effectiveTotalLines;

                        float startRatio = (float)windowOffset / (float)effectiveTotalLines;
                        float heightRatio = (float)visibleLines / (float)effectiveTotalLines;

                        // Clamp height ratio to max 1.0 (can't be larger than document)
                        if (heightRatio > 1.0f) heightRatio = 1.0f;

                        viewportY = (int)(startRatio * windowHeight);
                        viewportHeight = (int)(heightRatio * windowHeight);
                    }

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
                        HBITMAP amberBitmap = CreateCompatibleBitmap(hdc, windowWidth, viewportHeight);
                        if (amberBitmap) {
                            HBITMAP oldAmberBitmap = (HBITMAP)SelectObject(amberDC, amberBitmap);

                            // Fill viewport DC with gray color
                            HBRUSH amberBrush = CreateSolidBrush(RGB(201, 201, 201));
                            RECT amberRect = { 0, 0, windowWidth, viewportHeight };
                            FillRect(amberDC, &amberRect, amberBrush);
                            DeleteObject(amberBrush);

                            // Blend viewport box onto memory DC with 50% opacity
                            BLENDFUNCTION blend = { 0 };
                            blend.BlendOp = AC_SRC_OVER;
                            blend.BlendFlags = 0;
                            blend.SourceConstantAlpha = 128; // 50% opacity
                            blend.AlphaFormat = 0;

                            AlphaBlend(memDC, 0, viewportY, windowWidth, viewportHeight,
                                       amberDC, 0, 0, windowWidth, viewportHeight, blend);

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

                Rectangle(memDC, 0, 0, windowWidth, windowHeight);

                SelectObject(memDC, oldPen);
                SelectObject(memDC, oldBrush);
                DeleteObject(blackPen);

                // BitBlt the entire minimap to screen in one operation (eliminates flicker)
                BitBlt(hdc, minimapX, minimapY, windowWidth, windowHeight,
                       memDC, 0, 0, SRCCOPY);

                // Cleanup
                SelectObject(memDC, oldBitmap);
                DeleteObject(memBitmap);
            }
            DeleteDC(memDC);
        }

        EndPaint(minimapHwnd, &ps);
    }

    return 0;
}

LRESULT MinimapOverlay::HandlePaint(HWND hWnd, WPARAM wParam, LPARAM lParam)
{
    return HandlePaint(hWnd, hWnd, wParam, lParam);
}

LRESULT MinimapOverlay::HandleLeftButtonDown(HWND minimapHwnd, HWND scintillaHwnd, WPARAM wParam, LPARAM lParam)
{
    if (!minimapHwnd || !scintillaHwnd || !IsWindow(minimapHwnd) || !IsWindow(scintillaHwnd)) {
        return -1;
    }

    // Extract mouse coordinates from lParam
    int xPos = LOWORD(lParam);
    int yPos = HIWORD(lParam);

    // Get window dimensions to calculate minimap bounds
    RECT clientRect;
    GetClientRect(minimapHwnd, &clientRect);
    int windowHeight = clientRect.bottom;

    // Get Scintilla document metrics
    LRESULT totalLines = SendMessage(scintillaHwnd, SCI_GETLINECOUNT, 0, 0);
    LRESULT visibleLines = SendMessage(scintillaHwnd, SCI_LINESONSCREEN, 0, 0);
    LRESULT firstVisibleLine = SendMessage(scintillaHwnd, SCI_GETFIRSTVISIBLELINE, 0, 0);
    int lineHeight = GetLineHeight(scintillaHwnd);

    if (totalLines <= 0) {
        return 0;
    }

    // Determine rendering mode
    float optimalSpacing = (float)windowHeight / (float)totalLines;
    bool useAllLinesMode = (optimalSpacing >= 3.0f);

    int middleLine;
    if (useAllLinesMode) {
        // All lines mode: direct pixel-to-line mapping with actual row height
        // Must match rendering logic (40% of optimalSpacing, capped 2-8px, + 2px gap)
        int lineDrawHeight = (int)((float)optimalSpacing * 0.4f);
        if (lineDrawHeight < MINIMAP_NATURAL_LINE_HEIGHT) {
            lineDrawHeight = MINIMAP_NATURAL_LINE_HEIGHT;
        }
        if (lineDrawHeight > 8) {
            lineDrawHeight = 8;
        }
        int fixedGap = 2;
        int actualRowHeight = lineDrawHeight + fixedGap;

        middleLine = yPos / actualRowHeight;
        if (middleLine < 0) middleLine = 0;
        if (middleLine >= totalLines) middleLine = (int)totalLines - 1;
    } else {
        // Compressed mode: use window offset calculation
        int effectiveTotalLines = GetEffectiveTotalLines((int)totalLines, windowHeight, lineHeight, (int)visibleLines);
        if (effectiveTotalLines < 1) effectiveTotalLines = 1;

        int windowStart = GetStableMinimapWindowStart((int)totalLines, (int)visibleLines, effectiveTotalLines, (int)firstVisibleLine);

        // Calculate click position as percentage of minimap height
        float clickRatio = (float)yPos / (float)windowHeight;
        if (clickRatio < 0.0f) clickRatio = 0.0f;
        if (clickRatio > 1.0f) clickRatio = 1.0f;

        // Convert to middle line number (where user wants to center viewport)
        middleLine = windowStart + (int)(clickRatio * (float)effectiveTotalLines);
    }

    // Calculate target first visible line (center viewport on clicked position)
    int targetFirstVisible = middleLine - (visibleLines / 2);

    // Clamp to valid range [0, totalLines - visibleLines]
    if (targetFirstVisible < 0) targetFirstVisible = 0;
    int maxFirstVisible = (int)(totalLines - visibleLines);
    if (maxFirstVisible < 0) maxFirstVisible = 0;
    if (targetFirstVisible > maxFirstVisible) targetFirstVisible = maxFirstVisible;

    // Scroll to the calculated line
    SendMessage(scintillaHwnd, SCI_SETFIRSTVISIBLELINE, targetFirstVisible, 0);
    InvalidateRect(minimapHwnd, NULL, FALSE);

    // Consume the click event
    return 0;
}

LRESULT MinimapOverlay::HandleLeftButtonDown(HWND hWnd, WPARAM wParam, LPARAM lParam)
{
    return HandleLeftButtonDown(hWnd, hWnd, wParam, lParam);
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
