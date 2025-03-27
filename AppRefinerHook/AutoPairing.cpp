#include "AutoPairing.h"

// Global tracker for auto-inserted characters
AutoPairTracker g_autoPairTracker;

// Handle auto-pairing of quotes and parentheses
void HandleAutoPairing(HWND hwndScintilla, SCNotification* notification) {
    // Prevent recursive calls
    static bool isProcessing = false;
    if (isProcessing) {
        return;
    }
    
    // Add null checks
    if (!hwndScintilla || !IsWindow(hwndScintilla) || !notification) {
        return;
    }
    
    isProcessing = true;
    
    try {
        // If auto-pairing is disabled, do nothing
        if (!g_enableAutoPairing) {
            isProcessing = false;
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
                
                isProcessing = false;
                return;
            }
            // Otherwise, let the character be inserted normally
            isProcessing = false;
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
                    isProcessing = false;
                    return;
                }
            } else {
                // No quote ahead, insert a paired quote and position cursor between them
                SendMessage(hwndScintilla, SCI_ADDTEXT, 1, (LPARAM)"\"");
                SendMessage(hwndScintilla, SCI_SETSEL, currentPos, currentPos);
                // Track the auto-inserted character
                g_autoPairTracker.incrementCount('"');
                isProcessing = false;
                return;
            }
            
            // If we reach here, just let the quote be inserted normally
            isProcessing = false;
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
                    isProcessing = false;
                    return;
                }
                // Otherwise, let the character be inserted normally
                isProcessing = false;
                return;
            }
            // If no auto-inserted characters to consume, let the character be inserted normally
            isProcessing = false;
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
    catch (const std::exception& e) {
        char errorMsg[256];
        sprintf_s(errorMsg, "Exception in HandleAutoPairing: %s", e.what());
        OutputDebugStringA(errorMsg);
    }
    catch (...) {
        OutputDebugStringA("Unknown exception in HandleAutoPairing");
    }
    
    isProcessing = false;
}
