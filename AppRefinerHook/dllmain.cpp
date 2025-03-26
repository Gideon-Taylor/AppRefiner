#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <string>
#include <cctype>
#include <psapi.h>
#include <vector>
#include "Scintilla.h"

// Custom message for setting pipe name
#define WM_SET_CALLBACK_WINDOW (WM_USER + 1001)
// Message to toggle auto-pairing feature
#define WM_TOGGLE_AUTO_PAIRING (WM_USER + 1002)
// Message types for pipe communication
#define MSG_TYPE_DWELL 1

// Scintilla notifications
#define SCN_CHARADDED 2001
#define SCN_MODIFIED 2008
#define SCN_DWELLSTART 2016
#define SCN_DWELLEND 2017

/* TODO define messages with a mask to indicate "this is a scintilla event message" */
#define WM_SCN_EVENT_MASK 0x7000
#define WM_DWELL_START (WM_SCN_EVENT_MASK | SCN_DWELLSTART)
#define WM_DWELL_END (WM_SCN_EVENT_MASK | SCN_DWELLEND)

// Global variables
HWND g_callbackWindow = NULL;
HHOOK g_wndProcHook = NULL;
HHOOK g_getMsgHook = NULL;
HMODULE g_hModule = NULL;
bool g_enableAutoPairing = false;  // Flag to control auto-pairing feature

// Structure to track auto-inserted closing characters per line
struct AutoPairTracker {
    int lineNumber;                 // Line where auto-pairing occurred
    int quoteCount;                 // Count of auto-inserted double quotes
    int parenthesisCount;           // Count of auto-inserted closing parentheses
    
    AutoPairTracker() : lineNumber(-1), quoteCount(0), parenthesisCount(0) {}
    
    // Reset counts when line changes
    void checkLine(int newLine) {
        if (lineNumber != newLine) {
            lineNumber = newLine;
            quoteCount = 0;
            parenthesisCount = 0;
        }
    }
    
    // Increment count for a specific character
    void incrementCount(char ch) {
        if (ch == '"') quoteCount++;
        else if (ch == ')') parenthesisCount++;
    }
    
    // Decrement count for a specific character, returns true if there are auto-inserted characters to consume
    bool decrementCount(char ch) {
        if (ch == '"' && quoteCount > 0) {
            quoteCount--;
            return true;
        }
        else if (ch == ')' && parenthesisCount > 0) {
            parenthesisCount--;
            return true;
        }
        return false;
    }
};

// Global tracker for auto-inserted characters
AutoPairTracker g_autoPairTracker;

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

// Structure to hold block pattern information
struct BlockPattern {
    std::string startPattern;
    std::string endPattern;
    bool requiresFullMatch;
    bool requiresAdditionalCheck;
    std::string additionalPattern;
    bool decreasePreviousLine;  // Whether to decrease indentation of the previous line
    bool endPatternIsPartial;   // Whether the endPattern is a partial match (string starts with pattern)
    std::string matchingPattern; // The pattern this should be aligned with (for else, when, catch)
};

// Handle auto-pairing of quotes and parentheses
void HandleAutoPairing(HWND hwndScintilla, SCNotification* notification) {
    // If auto-pairing is disabled, do nothing
    if (!g_enableAutoPairing) {
        return;
    }

    // Get current position and line
    int currentPos = SendMessage(hwndScintilla, SCI_GETCURRENTPOS, 0, 0);
    int currentLine = SendMessage(hwndScintilla, SCI_LINEFROMPOSITION, currentPos, 0);
    
    // Update the tracker with the current line
    g_autoPairTracker.checkLine(currentLine);
    
    // Handle commas and semicolons - move them outside of auto-paired quotes
    if (notification->ch == ',' || notification->ch == ';') {
        // Get the character at the current position
        char nextChar = (char)SendMessage(hwndScintilla, SCI_GETCHARAT, currentPos, 0);
        
        // If the next character is a quote and we have an auto-paired quote to consume
        if (nextChar == '"' && g_autoPairTracker.quoteCount > 0) {
            // Delete the typed character (we'll reinsert it after the quote)
            SendMessage(hwndScintilla, SCI_DELETERANGE, currentPos - 1, 1);
            
            // Move cursor past the quote
            SendMessage(hwndScintilla, SCI_GOTOPOS, currentPos, 0);
            
            // Insert the comma or semicolon after the quote
            char charToInsert[2] = { notification->ch, 0 };
            SendMessage(hwndScintilla, SCI_ADDTEXT, 1, (LPARAM)charToInsert);
            
            // Decrement the quote count since we've effectively consumed it
            g_autoPairTracker.decrementCount('"');
            
            return;
        }
        // Otherwise, let the character be inserted normally
        return;
    }
    
    // Special handling for quotes since opening and closing are the same character
    if (notification->ch == '"') {
        // Get the character at the current position
        char nextChar = (char)SendMessage(hwndScintilla, SCI_GETCHARAT, currentPos, 0);
        
        // If the next character is a quote, we might want to skip over it instead of inserting a new one
        if (nextChar == '"') {
            // Check if this is an auto-inserted quote we should consume
            if (g_autoPairTracker.decrementCount('"')) {
                // Delete the typed quote (we'll skip over the existing one)
                SendMessage(hwndScintilla, SCI_DELETERANGE, currentPos - 1, 1);
                // Move cursor forward past the existing quote
                SendMessage(hwndScintilla, SCI_GOTOPOS, currentPos, 0);
                return;
            }
        } else {
            // No quote ahead, insert a paired quote and position cursor between them
            SendMessage(hwndScintilla, SCI_ADDTEXT, 1, (LPARAM)"\"");
            SendMessage(hwndScintilla, SCI_SETSEL, currentPos, currentPos);
            // Track the auto-inserted character
            g_autoPairTracker.incrementCount('"');
            return;
        }
        
        // If we reach here, just let the quote be inserted normally
        return;
    }
    
    // Check if we're overtyping a closing parenthesis
    if (notification->ch == ')') {
        // Check if we have auto-inserted characters to consume
        if (g_autoPairTracker.decrementCount(notification->ch)) {
            // Get the character at the current position
            char nextChar = (char)SendMessage(hwndScintilla, SCI_GETCHARAT, currentPos, 0);
            
            // If the next character matches what we just typed, consume it
            if (nextChar == notification->ch) {
                // Delete the typed character (it's already there)
                SendMessage(hwndScintilla, SCI_DELETERANGE, currentPos - 1, 1);
                // Move cursor forward
                SendMessage(hwndScintilla, SCI_GOTOPOS, currentPos, 0);
                return;
            }
            // Otherwise, let the character be inserted normally
            return;
        }
        // If no auto-inserted characters to consume, let the character be inserted normally
        return;
    }
    
    // Handle auto-pairing for opening characters
    switch (notification->ch) {
        case '(': {
            // Insert closing parenthesis and position cursor between parentheses
            SendMessage(hwndScintilla, SCI_ADDTEXT, 1, (LPARAM)")");
            SendMessage(hwndScintilla, SCI_SETSEL, currentPos, currentPos);
            // Track the auto-inserted character
            g_autoPairTracker.incrementCount(')');
            break;
        }
        // Add more cases for other pairs if needed
    }
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

    // Define block patterns for PeopleCode
    static const std::vector<BlockPattern> blockPatterns = {
        // Pattern, End Pattern, Requires Full Match, Requires Additional Check, Additional Pattern, Decrease Previous Line, End Pattern Is Partial, Matching Pattern
        {"if ", "end-if;", false, true, " then", false, false, "if "},
        {"for ", "end-for;", false, false, "", false, false, "for "},
        {"while ", "end-while;", false, false, "", false, false, "while "},
        {"method ", "end-method;", false, false, "", false, false, "method "},
        {"function ", "end-function;", false, false, "", false, false, "function "},
        {"else", "", true, false, "", true, false, "if "},  // 'else' has no end pattern but increases indentation and decreases previous line
        {"evaluate ", "end-evaluate;", false, false, "", false, false, "evaluate "},  // Evaluate block
        {"when ", "", false, false, "", true, false, "evaluate "},  // When clause should be at same level as Evaluate
        {"when-other", "", true, false, "", true, false, "evaluate "},  // When-Other should be at same level as Evaluate
        {"repeat", "until", true, false, "", false, true, "repeat"},  // Repeat/Until loop - uses partial matching for "until"
        {"try", "end-try;", true, false, "", false, false, "try"},  // Try block
        {"catch", "", false, false, "", true, false, "try"}  // Catch clause should be at same level as Try
    };

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
        bool decreasePreviousLine = false;

        // Check against all block start patterns
        for (const auto& pattern : blockPatterns) {
            bool matches = false;
            
            if (pattern.requiresFullMatch) {
                matches = (lowerLine == pattern.startPattern);
            } else {
                matches = (lowerLine.find(pattern.startPattern) == 0);
            }
            
            if (matches) {
                // Special case for method declarations in class headers
                // If the line starts with "method" and ends with semicolon, don't increase indentation
                if (pattern.startPattern == "method " && lowerLine.length() > 0 && lowerLine[lowerLine.length() - 1] == ';') {
                    continue;
                }
                
                // If additional check is required, verify it
                if (pattern.requiresAdditionalCheck) {
                    if (lowerLine.find(pattern.additionalPattern) != std::string::npos) {
                        increaseIndent = true;
                        decreasePreviousLine = pattern.decreasePreviousLine;
                        break;
                    }
                } else {
                    increaseIndent = true;
                    decreasePreviousLine = pattern.decreasePreviousLine;
                    break;
                }
            }
        }

        // If we need to decrease the indentation of the previous line (e.g., for "else")
        if (decreasePreviousLine) {
            // Find the matching pattern (if, evaluate, try) to get its indentation
            int searchLine = previousLine - 1;
            int matchingIndentation = 0;
            bool foundMatch = false;
            
            // Find which pattern we're looking for
            std::string patternToMatch = "";
            for (const auto& pattern : blockPatterns) {
                if (pattern.decreasePreviousLine) {
                    if ((pattern.requiresFullMatch && lowerLine == pattern.startPattern) ||
                        (!pattern.requiresFullMatch && lowerLine.find(pattern.startPattern) == 0)) {
                        patternToMatch = pattern.matchingPattern;
                        break;
                    }
                }
            }
            
            if (!patternToMatch.empty()) {
                // Track nesting level to handle nested blocks
                int nestingLevel = 0;
                
                while (searchLine >= 0) {
                    std::string searchLineStr = GetTrimmedLineText(hwndScintilla, searchLine);
                    std::string lowerSearchLine = ToLowerCase(searchLineStr);
                    int lineIndent = SendMessage(hwndScintilla, SCI_GETLINEINDENTATION, searchLine, 0);
                    
                    // Check for end statements that would increase our nesting level
                    for (const auto& pattern : blockPatterns) {
                        if (!pattern.endPattern.empty() && pattern.matchingPattern == patternToMatch) {
                            if (pattern.endPatternIsPartial) {
                                if (lowerSearchLine.find(pattern.endPattern) == 0 && lowerSearchLine.find(";") != std::string::npos) {
                                    nestingLevel++;
                                    break;
                                }
                            } else if (lowerSearchLine == pattern.endPattern) {
                                nestingLevel++;
                                break;
                            }
                        }
                    }
                    
                    // Check for matching opening statement
                    if (lowerSearchLine.find(patternToMatch) == 0) {
                        if (nestingLevel == 0) {
                            matchingIndentation = lineIndent;
                            foundMatch = true;
                            break;
                        }
                        nestingLevel--;
                    }
                    
                    searchLine--;
                }
                
                if (foundMatch) {
                    // Set the line indentation to match the opening statement
                    SendMessage(hwndScintilla, SCI_SETLINEINDENTATION, previousLine, matchingIndentation);
                    // Update our local indentation variable to match
                    indentation = matchingIndentation;
                }
            }
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
    else if (notification->ch == ';') {
        // Handle semicolon for end statements
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
        bool isEndStatement = false;
        std::string matchingStartPattern;
        
        // Check if the current line is an end statement
        for (const auto& pattern : blockPatterns) {
            if (!pattern.endPattern.empty()) {
                if (pattern.endPatternIsPartial) {
                    if (lowerCurrentLine.find(pattern.endPattern) == 0 && lowerCurrentLine.find(";") != std::string::npos) {
                        isEndStatement = true;
                        matchingStartPattern = pattern.startPattern;
                        shouldDeindent = true;
                        break;
                    }
                } else {
                    if (lowerCurrentLine == pattern.endPattern) {
                        isEndStatement = true;
                        matchingStartPattern = pattern.startPattern;
                        shouldDeindent = true;
                        break;
                    }
                }
            }
        }
        
        shouldDeindent = isEndStatement;

        if (shouldDeindent) {
            // For end statements and else, we need to find the matching opening statement
            // Instead of just using the previous line's indentation

            // First, find the block's starting line by searching backwards
            int openBlockLine = -1;
            int searchLine = currentLine - 1;
            int blockIndentation = 0;

            if (isEndStatement) {
                // For end statements, find the matching opening statement
                // Track nesting level to handle nested blocks
                int nestingLevel = 0;
                
                // Find which end statement we're dealing with
                std::string endPattern;
                std::string startPattern;
                bool requiresFullMatch = false;
                bool requiresAdditionalCheck = false;
                std::string additionalPattern;
                
                for (const auto& pattern : blockPatterns) {
                    if (!pattern.endPattern.empty()) {
                        if (pattern.endPatternIsPartial) {
                            if (lowerCurrentLine.find(pattern.endPattern) == 0 && lowerCurrentLine.find(";") != std::string::npos) {
                                endPattern = pattern.endPattern;
                                startPattern = pattern.startPattern;
                                requiresFullMatch = pattern.requiresFullMatch;
                                requiresAdditionalCheck = pattern.requiresAdditionalCheck;
                                additionalPattern = pattern.additionalPattern;
                                break;
                            }
                        } else {
                            if (lowerCurrentLine == pattern.endPattern) {
                                endPattern = pattern.endPattern;
                                startPattern = pattern.startPattern;
                                requiresFullMatch = pattern.requiresFullMatch;
                                requiresAdditionalCheck = pattern.requiresAdditionalCheck;
                                additionalPattern = pattern.additionalPattern;
                                break;
                            }
                        }
                    }
                }

                while (searchLine >= 0) {
                    std::string searchLineStr = GetTrimmedLineText(hwndScintilla, searchLine);
                    std::string lowerSearchLine = ToLowerCase(searchLineStr);
                    int lineIndent = SendMessage(hwndScintilla, SCI_GETLINEINDENTATION, searchLine, 0);

                    // Check for nested end statements that would increase our nesting level
                    if (lowerSearchLine == endPattern) {
                        nestingLevel++;
                    }
                    // Check for matching opening statement
                    else {
                        bool matches = false;
                        
                        if (requiresFullMatch) {
                            matches = (lowerSearchLine == startPattern);
                        } else {
                            matches = (lowerSearchLine.find(startPattern) == 0);
                        }
                        
                        if (matches) {
                            // If additional check is required, verify it
                            if (requiresAdditionalCheck) {
                                if (lowerSearchLine.find(additionalPattern) != std::string::npos) {
                                    if (nestingLevel == 0) {
                                        openBlockLine = searchLine;
                                        blockIndentation = lineIndent;
                                        break;
                                    }
                                    nestingLevel--;
                                }
                            } else {
                                if (nestingLevel == 0) {
                                    openBlockLine = searchLine;
                                    blockIndentation = lineIndent;
                                    break;
                                }
                                nestingLevel--;
                            }
                        }
                    }

                    searchLine--;
                }
            }
            else {
                // For else, find the matching if statement
                while (searchLine >= 0) {
                    std::string searchLineStr = GetTrimmedLineText(hwndScintilla, searchLine);
                    std::string lowerSearchLine = ToLowerCase(searchLineStr);
                    int lineIndent = SendMessage(hwndScintilla, SCI_GETLINEINDENTATION, searchLine, 0);

                    if (lowerSearchLine.find("if ") == 0 &&
                        lowerSearchLine.find(" then") != std::string::npos) {
                        openBlockLine = searchLine;
                        blockIndentation = lineIndent;
                        break;
                    }

                    searchLine--;
                }
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
                        // Handle auto-pairing first
                        HandleAutoPairing((HWND)nmhdr->hwndFrom, scn);
                        // Then handle auto-indentation
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
        // Check if this is our message to toggle auto-pairing
        else if (msg->message == WM_TOGGLE_AUTO_PAIRING) {
            g_enableAutoPairing = (msg->wParam != 0);
            char debugMsg[100];
            sprintf_s(debugMsg, "Auto-pairing %s\n", g_enableAutoPairing ? "enabled" : "disabled");
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
