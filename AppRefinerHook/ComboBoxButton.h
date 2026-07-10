#pragma once

#include <Windows.h>

#define SCI_INLAYHINTSSUPPORTED 2906

// ComboBox button functionality
// Adds a 24px button to the right of ComboBox controls in the dialog window
class ComboBoxButton
{
public:
    // Setup the button for a given Scintilla editor
    // Navigates: Scintilla -> Parent -> Sibling Dialog (#32770) -> ComboBoxes
    // Returns AR_SUB_ACK_* flags describing what was found/created (see Common.h)
    static DWORD Setup(HWND scintillaHwnd, HWND callbackWindow);

    // Remove the button and cleanup
    static void Cleanup(HWND scintillaHwnd);

    // Destroy the combo button on every registered combo dialog and unregister the
    // button window class. Call during DLL teardown, before the class can be unloaded.
    static void CleanupAll();

    // Sync a checkbox state on the combo button for the given Scintilla editor.
    // menuId: IDM_MINIMAP or IDM_PARAM_NAMES.  state: true = checked, false = unchecked.
    static void SyncCheckboxState(HWND scintillaHwnd, int menuId, bool state);

private:
    // Layout the ComboBoxes and button in the dialog
    static void LayoutDialog(HWND dialogHwnd, HWND callbackWindow);

    // Restore ComboBoxes to full width after the button is removed
    // Layout: [2px][COMBO1][4px][COMBO2][2px]
    static void RestoreLayout(HWND dialogHwnd);

    // Find the dialog window from Scintilla HWND
    static HWND FindDialogWindow(HWND scintillaHwnd);

    // Button window procedure
    static LRESULT CALLBACK ButtonWndProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam);

    // ComboBox subclass procedure (re-layout when ComboBox is resized)
    static LRESULT CALLBACK ComboBoxSubclassProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam,
        UINT_PTR uIdSubclass, DWORD_PTR dwRefData);

    // Dialog subclass procedure (handles WM_SIZE)
    static LRESULT CALLBACK DialogSubclassProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam,
        UINT_PTR uIdSubclass, DWORD_PTR dwRefData);
};
