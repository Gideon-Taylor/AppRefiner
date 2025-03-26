#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <string>
#include <cctype>
#include <psapi.h>
#include "Scintilla.h"

// Custom message for setting pipe name
#define WM_SET_CALLBACK_WINDOW (WM_USER + 1001)
// Message types for pipe communication
#define MSG_TYPE_DWELL 1

// Global variables
HWND g_callbackWindow = NULL;
HHOOK g_wndProcHook = NULL;
HHOOK g_getMsgHook = NULL;
HMODULE g_hModule = NULL;
#define SCN_CHARADDED 2001
#define SCN_MODIFIED 2008
#define SCN_DWELLSTART 2016
#define SCN_DWELLEND 2017

/* TODO define messages with a mask to indicate "this is a scintilla event message" */
#define WM_SCN_EVENT_MASK 0x7000
#define WM_DWELL_START (WM_SCN_EVENT_MASK | SCN_DWELLSTART)
#define WM_DWELL_END (WM_SCN_EVENT_MASK | SCN_DWELLEND)

// Helper function to convert string to lowercase
std::string ToLowerCase(const std::string& str) {
    std::string result = str;
    for (size_t i = 0; i < result.length(); ++i) {
        result[i] = std::tolower(result[i]);
    }
    return result;
}

// Helper function to get trimmed text of a line
std::string GetTrimmedLineText(HWND hwndScintilla, int line) {
    int lineLength = SendMessage(hwndScintilla, SCI_LINELENGTH, line, 0);
    if (lineLength <= 0) {
        return "";
    }

    char* lineText = new char[lineLength + 1];
    SendMessage(hwndScintilla, SCI_GETLINE, line, (LPARAM)lineText);
    lineText[lineLength] = '\0';

    std::string lineStr(lineText);
    delete[] lineText;

    // Trim leading whitespace
    size_t startPos = lineStr.find_first_not_of(" \t");
    if (startPos != std::string::npos) {
        lineStr = lineStr.substr(startPos);
    }
    else {
        lineStr = "";
    }

    // Trim trailing whitespace
    size_t endPos = lineStr.find_last_not_of(" \t\r\n");
    if (endPos != std::string::npos) {
        lineStr = lineStr.substr(0, endPos + 1);
    }

    return lineStr;
}

void HandlePeopleCodeAutoIndentation(HWND hwndScintilla, SCNotification* notification) {

    // Get the grandparent window
    HWND hwndGrandparent = GetParent(GetParent(hwndScintilla));
    if (hwndGrandparent == NULL) {
        return;
    }

    // Get the caption of the grandparent window
    char caption[256];
    GetWindowTextA(hwndGrandparent, caption, sizeof(caption));

    // Check if the caption contains "PeopleCode"
    if (!strstr(caption, "PeopleCode")) {
        return;
    }

    // Get the tab width for indentation units
    int tabWidth = SendMessage(hwndScintilla, SCI_GETTABWIDTH, 0, 0);

    // Handle indentation for new lines, else keyword, and semicolons differently
    if (notification->ch == '\r' || notification->ch == '\n') {
        // Get the current position and line
        int currentPos = SendMessage(hwndScintilla, SCI_GETCURRENTPOS, 0, 0);
        int currentLine = SendMessage(hwndScintilla, SCI_LINEFROMPOSITION, currentPos, 0);

        // If this is the first line, no indentation is needed
        if (currentLine <= 0) {
            return;
        }

        // Get previous line number
        int previousLine = currentLine - 1;

        // Get the previous line's text
        std::string prevLineStr = GetTrimmedLineText(hwndScintilla, previousLine);
        if (prevLineStr.empty()) {
            return;
        }

        // Get the indentation of the previous line
        int indentation = SendMessage(hwndScintilla, SCI_GETLINEINDENTATION, previousLine, 0);

        // Create a lowercase version for matching patterns
        std::string lowerLine = ToLowerCase(prevLineStr);

        // Check if previous line has an indentation-increasing statement
        bool increaseIndent = false;

        // Check for if-then
        if (lowerLine.find("if ") == 0 && lowerLine.find(" then") != std::string::npos) {
            increaseIndent = true;
        }
        // Check for for loops
        else if (lowerLine.find("for ") == 0) {
            increaseIndent = true;
        }
        // Check for while loops
        else if (lowerLine.find("while ") == 0) {
            increaseIndent = true;
        }
        // Check for method declarations
        else if (lowerLine.find("method ") == 0) {
            increaseIndent = true;
        }
        // Check for function declarations
        else if (lowerLine.find("function ") == 0) {
            increaseIndent = true;
        }
        // Check for else
        else if (lowerLine.find("else") == 0) {
            increaseIndent = true;
        }

        // Apply the indentation to the current line
        int newIndentation = indentation;
        if (increaseIndent) {
            // Increase indentation for blocks
            newIndentation += tabWidth;
        }

        // Set the indentation
        SendMessage(hwndScintilla, SCI_SETLINEINDENTATION, currentLine, newIndentation);

        // Move cursor to the end of indentation
        int newPos = SendMessage(hwndScintilla, SCI_GETLINEINDENTPOSITION, currentLine, 0);
        SendMessage(hwndScintilla, SCI_SETSEL, newPos, newPos);
    }
    else if (notification->ch == ';' || notification->ch == 'e') {
        // Handle semicolon for end statements or 'e' which might be part of "else"
        int currentPos = SendMessage(hwndScintilla, SCI_GETCURRENTPOS, 0, 0);
        int currentLine = SendMessage(hwndScintilla, SCI_LINEFROMPOSITION, currentPos, 0);

        // Get the current line's text
        std::string currentLineStr = GetTrimmedLineText(hwndScintilla, currentLine);
        if (currentLineStr.empty()) {
            return;
        }

        // Convert to lowercase for pattern matching
        std::string lowerCurrentLine = ToLowerCase(currentLineStr);

        // Check if this is an end statement or "else"
        bool shouldDeindent = false;
        bool isEndStatement = (lowerCurrentLine == "end-if;" ||
            lowerCurrentLine == "end-for;" ||
            lowerCurrentLine == "end-while;" ||
            lowerCurrentLine == "end-method;" ||
            lowerCurrentLine == "end-function;");

        bool isElse = (lowerCurrentLine == "else");

        shouldDeindent = isEndStatement || isElse;

        if (shouldDeindent) {
            // For end statements and else, we need to find the matching opening statement
            // Instead of just using the previous line's indentation

            // First, find the block's starting line by searching backwards
            int openBlockLine = -1;
            int searchLine = currentLine - 1;
            int blockIndentation = 0;

            while (searchLine >= 0) {
                std::string searchLineStr = GetTrimmedLineText(hwndScintilla, searchLine);
                std::string lowerSearchLine = ToLowerCase(searchLineStr);
                int lineIndent = SendMessage(hwndScintilla, SCI_GETLINEINDENTATION, searchLine, 0);

                if (isEndStatement) {
                    // For end statements, find the matching opening statement
                    if ((lowerCurrentLine == "end-if;" &&
                        lowerSearchLine.find("if ") == 0 &&
                        lowerSearchLine.find(" then") != std::string::npos) ||
                        (lowerCurrentLine == "end-for;" &&
                            lowerSearchLine.find("for ") == 0) ||
                        (lowerCurrentLine == "end-while;" &&
                            lowerSearchLine.find("while ") == 0) ||
                        (lowerCurrentLine == "end-method;" &&
                            lowerSearchLine.find("method ") == 0) ||
                        (lowerCurrentLine == "end-function;" &&
                            lowerSearchLine.find("function ") == 0)) {

                        openBlockLine = searchLine;
                        blockIndentation = lineIndent;
                        break;
                    }
                }
                else if (isElse) {
                    // For else, find the matching if statement
                    if (lowerSearchLine.find("if ") == 0 &&
                        lowerSearchLine.find(" then") != std::string::npos) {
                        openBlockLine = searchLine;
                        blockIndentation = lineIndent;
                        break;
                    }
                }

                searchLine--;
            }

            // Set indentation to match the opening statement
            if (openBlockLine >= 0) {
                SendMessage(hwndScintilla, SCI_SETLINEINDENTATION, currentLine, blockIndentation);
            }
            else {
                // Fallback - use current indentation minus tab width if no matching statement found
                int currentIndentation = SendMessage(hwndScintilla, SCI_GETLINEINDENTATION, currentLine, 0);
                int newIndentation = currentIndentation - tabWidth;
                if (newIndentation < 0) newIndentation = 0;
                SendMessage(hwndScintilla, SCI_SETLINEINDENTATION, currentLine, newIndentation);
            }
        }
    }
}

// WndProc hook procedure - for window messages
LRESULT CALLBACK WndProcHook(int nCode, WPARAM wParam, LPARAM lParam) {
    if (nCode >= 0) {
        CWPSTRUCT* cwp = (CWPSTRUCT*)lParam;

        // Check for WM_NOTIFY messages
        if (cwp->message == WM_NOTIFY) {
            // Get the NMHDR structure
            NMHDR* nmhdr = (NMHDR*)cwp->lParam;

            // Check if this is a Scintilla control
            char className[256] = { 0 };
            if (GetClassNameA((HWND)nmhdr->hwndFrom, className, sizeof(className)) > 0) {
                // Check if it's a Scintilla control (class name starts with "Scintilla")
                if (strncmp(className, "Scintilla", 9) == 0) {
                    // It's a Scintilla control, so we can treat lParam as SCNotification
                    SCNotification* scn = (SCNotification*)cwp->lParam;

                    if (scn->nmhdr.code == SCN_CHARADDED) {
                        HandlePeopleCodeAutoIndentation((HWND)nmhdr->hwndFrom, scn);
                        return CallNextHookEx(g_wndProcHook, nCode, wParam, lParam);
                    }

                    if (scn->nmhdr.code == SCN_DWELLSTART) {
                        SendMessage(g_callbackWindow, WM_DWELL_START, (WPARAM)scn->position, (LPARAM)0);
                    } 
                    else if (scn->nmhdr.code == SCN_DWELLEND) {
                        SendMessage(g_callbackWindow, WM_DWELL_END, (WPARAM)scn->position, (LPARAM)0);
                    }
                }
            }
        }
    }

    return CallNextHookEx(g_wndProcHook, nCode, wParam, lParam);
}

// GetMessage hook procedure - for thread messages
LRESULT CALLBACK GetMsgHook(int nCode, WPARAM wParam, LPARAM lParam) {
    if (nCode >= 0) {
        MSG* msg = (MSG*)lParam;

        // Check if this is our custom message for setting the callback window
        if (msg->message == WM_SET_CALLBACK_WINDOW) {
            g_callbackWindow = (HWND)msg->wParam;
            char debugMsg[100];
            sprintf_s(debugMsg, "Set callback window to: %p\n", g_callbackWindow);
            OutputDebugStringA(debugMsg);

            // Mark the message as handled
            msg->message = WM_NULL;
        }
    }

    return CallNextHookEx(g_getMsgHook, nCode, wParam, lParam);
}

// Export functions
extern "C" {
    __declspec(dllexport) HHOOK SetHook(DWORD threadId) {
        // Set both hooks for the specified thread
        g_wndProcHook = SetWindowsHookEx(WH_CALLWNDPROC, WndProcHook, g_hModule, threadId);
        g_getMsgHook = SetWindowsHookEx(WH_GETMESSAGE, GetMsgHook, g_hModule, threadId);

        // Return the WndProc hook as the primary hook handle
        return g_wndProcHook;
    }

    __declspec(dllexport) BOOL Unhook() {
        BOOL result = TRUE;

        if (g_wndProcHook != NULL) {
            result = UnhookWindowsHookEx(g_wndProcHook) && result;
            g_wndProcHook = NULL;
        }

        if (g_getMsgHook != NULL) {
            result = UnhookWindowsHookEx(g_getMsgHook) && result;
            g_getMsgHook = NULL;
        }

        return result;
    }
}

// DLL entry point
BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
    switch (ul_reason_for_call) {
    case DLL_PROCESS_ATTACH:
        g_hModule = hModule;
        DisableThreadLibraryCalls(hModule);
        break;
    case DLL_PROCESS_DETACH:
        // Clean up resources
        Unhook();
        break;
    }
    return TRUE;
}
