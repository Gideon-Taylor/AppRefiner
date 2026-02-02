#include "ComboBoxButton.h"
#include "Common.h"
#include "Resource.h"
#include "MinimapManager.h"
#include <vector>
#include <commctrl.h>

// Constants
static const wchar_t* COMBO_BUTTON_CLASS = L"AppRefinerComboButton";
static const wchar_t* COMBO_BUTTON_PROP = L"AR_ComboButtonHwnd";
static const wchar_t* BUTTON_PRESSED_PROP = L"AR_ButtonPressed";
static const wchar_t* BUTTON_SCINTILLA_PROP = L"AR_ButtonScintillaHwnd"; // Store Scintilla HWND for minimap toggle
static const wchar_t* LAYOUT_TIMER_PROP = L"AR_LayoutTimer";
static const int COMBO_BUTTON_WIDTH = 24;
static const UINT_PTR COMBO_DIALOG_SUBCLASS_ID = 4; // Unique subclass ID
static const UINT_PTR LAYOUT_TIMER_ID = 100; // Timer ID for delayed layout

// External reference to module handle (defined in HookManager.cpp)
extern HMODULE g_hModule;

// Helper structure for EnumChildWindows
struct FindDialogData {
    HWND dialogHwnd;
    HWND excludeHwnd; // HWND to exclude from search
};

// Button window procedure
LRESULT CALLBACK ComboBoxButton::ButtonWndProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam)
{
    switch (uMsg) {
        case WM_LBUTTONDOWN:
        {
            // Set pressed state
            SetPropW(hWnd, BUTTON_PRESSED_PROP, (HANDLE)1);

            // Capture mouse so we get WM_LBUTTONUP even if cursor moves outside
            SetCapture(hWnd);

            // Redraw button in pressed state
            InvalidateRect(hWnd, NULL, FALSE);
            return 0;
        }

        case WM_LBUTTONUP:
        {
            // Check if button was pressed
            bool wasPressed = (GetPropW(hWnd, BUTTON_PRESSED_PROP) != NULL);

            // Clear pressed state
            RemovePropW(hWnd, BUTTON_PRESSED_PROP);

            // Release mouse capture
            ReleaseCapture();

            // Redraw button in normal state
            InvalidateRect(hWnd, NULL, FALSE);

            // Only toggle minimap if button was actually pressed
            if (wasPressed) {
                // Check if mouse is still over button
                POINT pt;
                GetCursorPos(&pt);
                ScreenToClient(hWnd, &pt);

                RECT rect;
                GetClientRect(hWnd, &rect);

                if (PtInRect(&rect, pt)) {
                    // Get the Scintilla HWND and callback window
                    HWND scintillaHwnd = (HWND)GetPropW(hWnd, BUTTON_SCINTILLA_PROP);
                    HWND callbackWindow = (HWND)GetWindowLongPtr(hWnd, GWLP_USERDATA);

                    if (scintillaHwnd && IsWindow(scintillaHwnd)) {
                        OutputDebugStringA("ComboBox button clicked - toggling minimap");
                        bool isEnabled = MinimapManager::ToggleMinimap(scintillaHwnd, callbackWindow);

                        char debugMsg[256];
                        sprintf_s(debugMsg, "Minimap %s for Scintilla: 0x%p\n",
                                 isEnabled ? "enabled" : "disabled", scintillaHwnd);
                        OutputDebugStringA(debugMsg);
                    } else {
                        OutputDebugStringA("ComboBox button clicked - invalid Scintilla HWND");
                    }
                }
            }
            return 0;
        }

        case WM_MOUSELEAVE:
        {
            // Clear pressed state if mouse leaves while pressed
            bool wasPressed = (GetPropW(hWnd, BUTTON_PRESSED_PROP) != NULL);
            if (wasPressed) {
                RemovePropW(hWnd, BUTTON_PRESSED_PROP);
                InvalidateRect(hWnd, NULL, FALSE);
            }
            return 0;
        }

        case WM_PAINT:
        {
            PAINTSTRUCT ps;
            HDC hdc = BeginPaint(hWnd, &ps);
            if (hdc) {
                RECT rect;
                GetClientRect(hWnd, &rect);

                // Check if button is pressed
                bool isPressed = (GetPropW(hWnd, BUTTON_PRESSED_PROP) != NULL);

                // Draw button face with system colors
                FillRect(hdc, &rect, (HBRUSH)(COLOR_BTNFACE + 1));

                // Draw edge based on pressed state
                DrawEdge(hdc, &rect, isPressed ? EDGE_SUNKEN : EDGE_RAISED, BF_RECT);

                // Draw icon from resource (16x16 recommended for 24px button)
                HICON hIcon = LoadIcon(g_hModule, MAKEINTRESOURCE(IDI_COMBO_BUTTON_ICON));
                if (hIcon) {
                    // Center the icon in the button
                    int iconSize = 16; // Standard small icon size
                    int x = (rect.right - iconSize) / 2;
                    int y = (rect.bottom - iconSize) / 2;

                    // Offset icon slightly when pressed for visual feedback
                    if (isPressed) {
                        x += 1;
                        y += 1;
                    }

                    DrawIconEx(hdc, x, y, hIcon, iconSize, iconSize, 0, NULL, DI_NORMAL);
                    DestroyIcon(hIcon);
                } else {
                    // Fallback if icon not found
                    SetBkMode(hdc, TRANSPARENT);
                    DrawTextA(hdc, "?", -1, &rect, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
                }

                EndPaint(hWnd, &ps);
            }
            return 0;
        }

        case WM_ERASEBKGND:
            // Prevent flicker by handling erase in WM_PAINT
            return 1;
    }

    return DefWindowProc(hWnd, uMsg, wParam, lParam);
}

// ComboBox subclass procedure (re-layout when ComboBox is resized)
LRESULT CALLBACK ComboBoxButton::ComboBoxSubclassProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam,
    UINT_PTR uIdSubclass, DWORD_PTR dwRefData)
{
    if (uMsg == WM_WINDOWPOSCHANGING) {
        // Allow default processing first
        LRESULT result = DefSubclassProc(hWnd, uMsg, wParam, lParam);

        // After ComboBox position is changed, re-layout the dialog
        HWND dialogHwnd = GetParent(hWnd);
        if (dialogHwnd && IsWindow(dialogHwnd)) {
            HWND callbackWindow = (HWND)dwRefData;
            LayoutDialog(dialogHwnd, callbackWindow);
        }

        return result;
    }

    if (uMsg == WM_NCDESTROY) {
        // Remove subclass on destroy
        RemoveWindowSubclass(hWnd, ComboBoxSubclassProc, uIdSubclass);
    }

    return DefSubclassProc(hWnd, uMsg, wParam, lParam);
}

// Dialog subclass procedure (handles WM_SIZE and cleanup)
LRESULT CALLBACK ComboBoxButton::DialogSubclassProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam,
    UINT_PTR uIdSubclass, DWORD_PTR dwRefData)
{
    if (uMsg == WM_NCDESTROY) {
        // Kill any pending layout timer
        KillTimer(hWnd, LAYOUT_TIMER_ID);

        // Cleanup: destroy button and remove properties
        HWND buttonHwnd = (HWND)GetPropW(hWnd, COMBO_BUTTON_PROP);
        if (buttonHwnd && IsWindow(buttonHwnd)) {
            // Clean up button's pressed state property
            RemovePropW(buttonHwnd, BUTTON_PRESSED_PROP);
            DestroyWindow(buttonHwnd);
        }
        RemovePropW(hWnd, COMBO_BUTTON_PROP);
        RemoveWindowSubclass(hWnd, DialogSubclassProc, COMBO_DIALOG_SUBCLASS_ID);
        OutputDebugStringA("ComboBox dialog destroyed - cleaned up button");
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    if (uMsg == WM_SIZE) {
        // Use a timer to delay layout until all resize operations are complete
        SetTimer(hWnd, LAYOUT_TIMER_ID, 10, NULL); // 10ms delay
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    if (uMsg == WM_WINDOWPOSCHANGED) {
        // Use a timer to delay layout until all positioning is complete
        SetTimer(hWnd, LAYOUT_TIMER_ID, 10, NULL); // 10ms delay
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    if (uMsg == WM_TIMER && wParam == LAYOUT_TIMER_ID) {
        // Kill the timer so it only fires once
        KillTimer(hWnd, LAYOUT_TIMER_ID);

        // Now perform the layout
        HWND callbackWindow = (HWND)dwRefData;
        LayoutDialog(hWnd, callbackWindow);
        return 0;
    }

    return DefSubclassProc(hWnd, uMsg, wParam, lParam);
}

// Find the dialog window from Scintilla HWND
HWND ComboBoxButton::FindDialogWindow(HWND scintillaHwnd)
{
    if (!scintillaHwnd || !IsWindow(scintillaHwnd)) {
        return NULL;
    }

    // Navigate: Scintilla -> Parent -> Sibling Dialog (#32770)
    HWND parentHwnd = GetParent(scintillaHwnd);
    if (!parentHwnd || !IsWindow(parentHwnd)) {
        OutputDebugStringA("FindDialogWindow: No parent found for Scintilla");
        return NULL;
    }

    // Get grandparent to enumerate siblings
    HWND grandParent = GetParent(parentHwnd);
    if (!grandParent || !IsWindow(grandParent)) {
        OutputDebugStringA("FindDialogWindow: No grandparent found");
        return NULL;
    }

    // Find sibling dialog window (class #32770)
    FindDialogData findData = { NULL, parentHwnd };
    EnumChildWindows(grandParent, [](HWND hWnd, LPARAM lParam) -> BOOL {
        FindDialogData* data = (FindDialogData*)lParam;

        // Skip the parent window itself
        if (hWnd == data->excludeHwnd) {
            return TRUE;
        }

        char className[256] = { 0 };
        if (GetClassNameA(hWnd, className, sizeof(className)) > 0) {
            // Dialog windows have class name "#32770"
            if (strcmp(className, "#32770") == 0) {
                data->dialogHwnd = hWnd;

                char debugMsg[256];
                sprintf_s(debugMsg, "FindDialogWindow: Found dialog window: 0x%p", hWnd);
                OutputDebugStringA(debugMsg);

                return FALSE; // Stop enumeration
            }
        }
        return TRUE; // Continue enumeration
    }, (LPARAM)&findData);

    return findData.dialogHwnd;
}

// Layout the ComboBoxes and button in the dialog
void ComboBoxButton::LayoutDialog(HWND dialogHwnd, HWND callbackWindow)
{
    if (!dialogHwnd || !IsWindow(dialogHwnd)) {
        return;
    }

    // Re-entrancy guard to prevent infinite loop
    static bool isLayoutInProgress = false;
    if (isLayoutInProgress) {
        return; // Already laying out, don't recurse
    }

    // Set guard
    isLayoutInProgress = true;

    // Get dialog client dimensions
    RECT dialogRect;
    GetClientRect(dialogHwnd, &dialogRect);
    int dialogWidth = dialogRect.right;

    // Find all ComboBox controls in the dialog
    std::vector<HWND> comboBoxes;
    EnumChildWindows(dialogHwnd, [](HWND hWnd, LPARAM lParam) -> BOOL {
        auto* combos = (std::vector<HWND>*)lParam;
        char className[256] = { 0 };
        if (GetClassNameA(hWnd, className, sizeof(className)) > 0) {
            if (strcmp(className, "ComboBox") == 0) {
                combos->push_back(hWnd);
            }
        }
        return TRUE; // Continue enumeration
    }, (LPARAM)&comboBoxes);

    if (comboBoxes.size() < 2) {
        // Not enough ComboBoxes found - might not be the right dialog
        char debugMsg[256];
        sprintf_s(debugMsg, "LayoutDialog: Found %zu ComboBoxes (expected 2)", comboBoxes.size());
        OutputDebugStringA(debugMsg);
        isLayoutInProgress = false; // Clear guard before early return
        return;
    }

    // Get or create the button
    HWND buttonHwnd = (HWND)GetPropW(dialogHwnd, COMBO_BUTTON_PROP);
    if (!buttonHwnd || !IsWindow(buttonHwnd)) {
        // Register button window class if not already registered
        WNDCLASSW wc = { 0 };
        if (!GetClassInfoW(g_hModule, COMBO_BUTTON_CLASS, &wc)) {
            wc.lpfnWndProc = ButtonWndProc;
            wc.hInstance = g_hModule;
            wc.lpszClassName = COMBO_BUTTON_CLASS;
            wc.hCursor = LoadCursor(NULL, IDC_ARROW);
            wc.hbrBackground = (HBRUSH)(COLOR_BTNFACE + 1);
            RegisterClassW(&wc);
            OutputDebugStringA("Registered ComboBox button window class");
        }

        // Create button as child of dialog
        buttonHwnd = CreateWindowExW(
            0,
            COMBO_BUTTON_CLASS,
            L"",
            WS_CHILD | WS_VISIBLE,
            0, 0, COMBO_BUTTON_WIDTH, 0,
            dialogHwnd,
            NULL,
            g_hModule,
            NULL
        );

        if (buttonHwnd) {
            // Store callback window in button's GWLP_USERDATA
            SetWindowLongPtr(buttonHwnd, GWLP_USERDATA, (LONG_PTR)callbackWindow);
            // Associate button with dialog using window property
            SetPropW(dialogHwnd, COMBO_BUTTON_PROP, buttonHwnd);

            // Note: Scintilla HWND will be set during Setup() call
            char debugMsg[256];
            sprintf_s(debugMsg, "Created ComboBox button: 0x%p", buttonHwnd);
            OutputDebugStringA(debugMsg);
        } else {
            DWORD error = GetLastError();
            char errorMsg[256];
            sprintf_s(errorMsg, "Failed to create ComboBox button, error: %lu", error);
            OutputDebugStringA(errorMsg);
            isLayoutInProgress = false; // Clear guard before early return
            return;
        }
    }

    // Padding constants
    const int EDGE_PADDING = 2;       // 2px padding from window edges
    const int CONTROL_SPACING = 4;    // 4px spacing between controls

    // Calculate layout: [2px][COMBO1][4px][COMBO2][4px][BUTTON][2px]
    // Total consumed by padding/spacing/button: 2 + 4 + 4 + 24 + 2 = 36px
    int totalReserved = (EDGE_PADDING * 2) + (CONTROL_SPACING * 2) + COMBO_BUTTON_WIDTH;
    int availableWidth = dialogWidth - totalReserved;
    int comboWidth = availableWidth / 2;

    // Get position and height from first ComboBox
    RECT comboRect;
    GetWindowRect(comboBoxes[0], &comboRect);
    int comboHeight = comboRect.bottom - comboRect.top;

    // Convert screen coords to client coords
    POINT comboPos = { comboRect.left, comboRect.top };
    ScreenToClient(dialogHwnd, &comboPos);
    int comboY = comboPos.y;

    // Calculate X positions with padding
    int combo1X = EDGE_PADDING;
    int combo2X = combo1X + comboWidth + CONTROL_SPACING;
    int buttonX = combo2X + comboWidth + CONTROL_SPACING;

    // Position first ComboBox
    SetWindowPos(comboBoxes[0], NULL,
        combo1X, comboY, comboWidth, comboHeight,
        SWP_NOZORDER | SWP_NOACTIVATE);

    // Position second ComboBox
    SetWindowPos(comboBoxes[1], NULL,
        combo2X, comboY, comboWidth, comboHeight,
        SWP_NOZORDER | SWP_NOACTIVATE);

    // Position button
    if (buttonHwnd) {
        SetWindowPos(buttonHwnd, NULL,
            buttonX, comboY, COMBO_BUTTON_WIDTH, comboHeight,
            SWP_NOZORDER | SWP_NOACTIVATE);
    }

    char debugMsg[256];
    sprintf_s(debugMsg, "Layout complete - Dialog width: %d, Combo width: %d each (edge padding: %dpx, spacing: %dpx)",
        dialogWidth, comboWidth, EDGE_PADDING, CONTROL_SPACING);
    OutputDebugStringA(debugMsg);

    // Clear re-entrancy guard
    isLayoutInProgress = false;
}

// Setup the button for a given Scintilla editor
void ComboBoxButton::Setup(HWND scintillaHwnd, HWND callbackWindow)
{
    if (!scintillaHwnd || !IsWindow(scintillaHwnd)) {
        OutputDebugStringA("ComboBoxButton::Setup - Invalid Scintilla HWND");
        return;
    }

    if (!callbackWindow || !IsWindow(callbackWindow)) {
        OutputDebugStringA("ComboBoxButton::Setup - Invalid callback window");
        return;
    }

    // Find the dialog window
    HWND dialogHwnd = FindDialogWindow(scintillaHwnd);
    if (!dialogHwnd) {
        OutputDebugStringA("ComboBoxButton::Setup - Dialog window not found");
        return;
    }

    // Check if already subclassed
    DWORD_PTR existingData = 0;
    if (GetWindowSubclass(dialogHwnd, DialogSubclassProc, COMBO_DIALOG_SUBCLASS_ID, &existingData)) {
        OutputDebugStringA("ComboBoxButton::Setup - Dialog already subclassed");
        return;
    }

    // Subclass the dialog to handle WM_SIZE
    if (SetWindowSubclass(dialogHwnd, DialogSubclassProc, COMBO_DIALOG_SUBCLASS_ID, (DWORD_PTR)callbackWindow)) {
        OutputDebugStringA("ComboBoxButton::Setup - Subclassed dialog window");

        // Perform initial layout
        LayoutDialog(dialogHwnd, callbackWindow);

        // Store Scintilla HWND in button's properties for minimap toggle
        HWND buttonHwnd = (HWND)GetPropW(dialogHwnd, COMBO_BUTTON_PROP);
        if (buttonHwnd && IsWindow(buttonHwnd)) {
            SetPropW(buttonHwnd, BUTTON_SCINTILLA_PROP, scintillaHwnd);

            char debugMsg[256];
            sprintf_s(debugMsg, "Stored Scintilla HWND 0x%p in button for minimap toggle\n", scintillaHwnd);
            OutputDebugStringA(debugMsg);
        }
    } else {
        OutputDebugStringA("ComboBoxButton::Setup - Failed to subclass dialog");
    }
}

// Remove the button and cleanup
void ComboBoxButton::Cleanup(HWND scintillaHwnd)
{
    if (!scintillaHwnd || !IsWindow(scintillaHwnd)) {
        return;
    }

    HWND dialogHwnd = FindDialogWindow(scintillaHwnd);
    if (!dialogHwnd) {
        return;
    }

    // Remove subclass (this will trigger cleanup in WM_NCDESTROY equivalent)
    RemoveWindowSubclass(dialogHwnd, DialogSubclassProc, COMBO_DIALOG_SUBCLASS_ID);

    // Manual cleanup in case subclass wasn't active
    HWND buttonHwnd = (HWND)GetPropW(dialogHwnd, COMBO_BUTTON_PROP);
    if (buttonHwnd && IsWindow(buttonHwnd)) {
        // Clean up button's pressed state property
        RemovePropW(buttonHwnd, BUTTON_PRESSED_PROP);
        DestroyWindow(buttonHwnd);
    }
    RemovePropW(dialogHwnd, COMBO_BUTTON_PROP);

    OutputDebugStringA("ComboBoxButton::Cleanup - Button removed");
}
