#pragma once

#include "Common.h"
#include "ScintillaUtils.h"

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

// Handle PeopleCode auto-indentation
void HandlePeopleCodeAutoIndentation(HWND hwndScintilla, SCNotification* notification);
