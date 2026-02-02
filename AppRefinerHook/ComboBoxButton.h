#pragma once

#include <Windows.h>

// ComboBox button functionality
// Adds a 24px button to the right of ComboBox controls in the dialog window
class ComboBoxButton
{
public:
    // Setup the button for a given Scintilla editor
    // Navigates: Scintilla -> Parent -> Sibling Dialog (#32770) -> ComboBoxes
    static void Setup(HWND scintillaHwnd, HWND callbackWindow);

    // Remove the button and cleanup
    static void Cleanup(HWND scintillaHwnd);

private:
    // Layout the ComboBoxes and button in the dialog
    static void LayoutDialog(HWND dialogHwnd, HWND callbackWindow);

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
