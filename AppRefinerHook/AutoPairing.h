#pragma once

#include "Common.h"

// Structure to track auto-inserted closing characters per line
struct AutoPairTracker {
    int lineNumber;                 // Line where auto-pairing occurred
    int quoteCount;                 // Count of auto-inserted double quotes
    int parenthesisCount;           // Count of auto-inserted closing parentheses
    
    AutoPairTracker() : lineNumber(-1), quoteCount(0), parenthesisCount(0) {}
    
    // Reset all counts and line information (used when switching editors)
    void reset() {
        lineNumber = -1;
        quoteCount = 0;
        parenthesisCount = 0;
    }
    
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

// Global tracker for auto-inserted characters (defined in AutoPairing.cpp)
extern AutoPairTracker g_autoPairTracker;

// Handle auto-pairing of quotes and parentheses
void HandleAutoPairing(HWND hwndScintilla, SCNotification* notification);
