#include "AutoIndent.h"

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

void HandlePeopleCodeAutoIndentation(HWND hwndScintilla, SCNotification* notification) {
    // Prevent recursive calls
    static bool isProcessing = false;
    if (isProcessing) {
        return;
    }
    
    // Add null check for Scintilla control
    if (!hwndScintilla || !IsWindow(hwndScintilla) || !notification) {
        return;
    }
    
    isProcessing = true;
    
    try {
        // Get the grandparent window
        HWND hwndGrandparent = GetParent(GetParent(hwndScintilla));
        if (hwndGrandparent == NULL || !IsWindow(hwndGrandparent)) {
            isProcessing = false;
            return;
        }

        // Get the caption of the grandparent window
        char caption[256] = {0};
        if (GetWindowTextA(hwndGrandparent, caption, sizeof(caption)) == 0) {
            // Failed to get window text
            isProcessing = false;
            return;
        }

        // Check if the caption contains "PeopleCode"
        if (!strstr(caption, "PeopleCode")) {
            isProcessing = false;
            return;
        }

        // Get the tab width for indentation units
        int tabWidth = SendMessage(hwndScintilla, SCI_GETTABWIDTH, 0, 0);
        if (tabWidth <= 0) {
            // Use a default tab width if the control returns an invalid value
            tabWidth = 4;
        }

        // Handle indentation for new lines, else keyword, and semicolons differently
        if (notification->ch == '\r' || notification->ch == '\n') {
            // Get the current position and line
            int currentPos = SendMessage(hwndScintilla, SCI_GETCURRENTPOS, 0, 0);
            if (currentPos < 0) {
                isProcessing = false;
                return;
            }
            
            int currentLine = SendMessage(hwndScintilla, SCI_LINEFROMPOSITION, currentPos, 0);
            if (currentLine < 0) {
                isProcessing = false;
                return;
            }

            // If this is the first line, no indentation is needed
            if (currentLine <= 0) {
                isProcessing = false;
                return;
            }

            // Get previous line number
            int previousLine = currentLine - 1;

            // Get the previous line's text
            std::string prevLineStr = GetTrimmedLineText(hwndScintilla, previousLine);
            if (prevLineStr.empty()) {
                isProcessing = false;
                return;
            }

            // Get the indentation of the previous line
            int indentation = SendMessage(hwndScintilla, SCI_GETLINEINDENTATION, previousLine, 0);
            if (indentation < 0) {
                indentation = 0;
            }

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
                    
                    // Add safety counter to prevent infinite loops
                    const int MAX_ITERATIONS = 1000;
                    int iterations = 0;
                    
                    while (searchLine >= 0 && iterations < MAX_ITERATIONS) {
                        if (!IsWindow(hwndScintilla)) {
                            // Scintilla control has been destroyed
                            isProcessing = false;
                            return;
                        }
                        
                        std::string searchLineStr = GetTrimmedLineText(hwndScintilla, searchLine);
                        std::string lowerSearchLine = ToLowerCase(searchLineStr);
                        int lineIndent = SendMessage(hwndScintilla, SCI_GETLINEINDENTATION, searchLine, 0);
                        if (lineIndent < 0) lineIndent = 0;
                        
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
                        iterations++;
                    }
                    
                    // Check if we hit the iteration limit
                    if (iterations >= MAX_ITERATIONS) {
                        OutputDebugStringA("Warning: Reached maximum iterations in pattern matching loop");
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
            if (currentPos < 0) {
                isProcessing = false;
                return;
            }
            
            int currentLine = SendMessage(hwndScintilla, SCI_LINEFROMPOSITION, currentPos, 0);
            if (currentLine < 0) {
                isProcessing = false;
                return;
            }

            // Get the current line's text
            std::string currentLineStr = GetTrimmedLineText(hwndScintilla, currentLine);
            if (currentLineStr.empty()) {
                isProcessing = false;
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
                if (searchLine < 0) {
                    isProcessing = false;
                    return;
                }
                
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

                    // Add safety counter to prevent infinite loops
                    const int MAX_ITERATIONS = 1000;
                    int iterations = 0;

                    while (searchLine >= 0 && iterations < MAX_ITERATIONS) {
                        if (!IsWindow(hwndScintilla)) {
                            // Scintilla control has been destroyed
                            isProcessing = false;
                            return;
                        }
                        
                        std::string searchLineStr = GetTrimmedLineText(hwndScintilla, searchLine);
                        std::string lowerSearchLine = ToLowerCase(searchLineStr);
                        int lineIndent = SendMessage(hwndScintilla, SCI_GETLINEINDENTATION, searchLine, 0);
                        if (lineIndent < 0) lineIndent = 0;

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
                        iterations++;
                    }
                    
                    // Check if we hit the iteration limit
                    if (iterations >= MAX_ITERATIONS) {
                        OutputDebugStringA("Warning: Reached maximum iterations in end statement matching loop");
                    }
                }
                else {
                    // For else, find the matching if statement
                    // Add safety counter to prevent infinite loops
                    const int MAX_ITERATIONS = 1000;
                    int iterations = 0;
                    
                    while (searchLine >= 0 && iterations < MAX_ITERATIONS) {
                        if (!IsWindow(hwndScintilla)) {
                            // Scintilla control has been destroyed
                            isProcessing = false;
                            return;
                        }
                        
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
                        iterations++;
                    }
                    
                    // Check if we hit the iteration limit
                    if (iterations >= MAX_ITERATIONS) {
                        OutputDebugStringA("Warning: Reached maximum iterations in else statement matching loop");
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
        else if (notification->ch == 'f') {
            // Handle "else if" expansion
            int currentPos = SendMessage(hwndScintilla, SCI_GETCURRENTPOS, 0, 0);
            int currentLine = SendMessage(hwndScintilla, SCI_LINEFROMPOSITION, currentPos, 0);

            // Get the current line's text
            std::string currentLineStr = GetTrimmedLineText(hwndScintilla, currentLine);
            if (currentLineStr.empty()) {
                return;
            }

            // Convert to lowercase for pattern matching
            std::string lowerCurrentLine = ToLowerCase(currentLineStr);

            // Check if the current line is exactly "else if"
            if (lowerCurrentLine == "else if") {
                // Begin undo action
                SendMessage(hwndScintilla, SCI_BEGINUNDOACTION, 0, 0);

                try {
                    // Get the indentation of the current line
                    int currentIndentation = SendMessage(hwndScintilla, SCI_GETLINEINDENTATION, currentLine, 0);
                    int tabWidth = SendMessage(hwndScintilla, SCI_GETTABWIDTH, 0, 0);

                    // Delete the current line
                    int lineStartPos = SendMessage(hwndScintilla, SCI_POSITIONFROMLINE, currentLine, 0);
                    int lineEndPos = SendMessage(hwndScintilla, SCI_GETLINEENDPOSITION, currentLine, 0);
                    SendMessage(hwndScintilla, SCI_DELETERANGE, lineStartPos, lineEndPos - lineStartPos);

                    // Calculate indentation strings
                    // We need to de-indent the Else and End-if by one level since we're already in an indented block
                    int baseIndentation = currentIndentation - tabWidth;
                    if (baseIndentation < 0) baseIndentation = 0;
                    
                    std::string baseIndentStr(baseIndentation / tabWidth, '\t');
                    std::string ifIndentStr((baseIndentation / tabWidth) + 1, '\t');
                    
                    // Create the expanded text with proper indentation
                    std::string expandedText = baseIndentStr + "Else\n" + 
                                              ifIndentStr + "If \n" + 
                                              baseIndentStr + "End-if;";

                    // Insert the expanded text
                    SendMessage(hwndScintilla, SCI_INSERTTEXT, lineStartPos, (LPARAM)expandedText.c_str());

                    // Position the cursor after "If " (before the newline)
                    int ifLineLength = ifIndentStr.length() + 3; // "If " is 3 characters
                    int newCursorPos = lineStartPos + baseIndentStr.length() + 5 + ifLineLength; // "Else\n" is 5+1 characters
                    SendMessage(hwndScintilla, SCI_SETSEL, newCursorPos, newCursorPos);
                }
                catch (const std::exception& e) {
                    char errorMsg[256];
                    sprintf_s(errorMsg, "Exception in else-if expansion: %s", e.what());
                    OutputDebugStringA(errorMsg);
                }
                catch (...) {
                    OutputDebugStringA("Unknown exception in else-if expansion");
                }

                // End undo action
                SendMessage(hwndScintilla, SCI_ENDUNDOACTION, 0, 0);
            }
        }
    }
    catch (const std::exception& e) {
        char errorMsg[256];
        sprintf_s(errorMsg, "Exception in HandlePeopleCodeAutoIndentation: %s", e.what());
        OutputDebugStringA(errorMsg);
    }
    catch (...) {
        OutputDebugStringA("Unknown exception in HandlePeopleCodeAutoIndentation");
    }
    
    isProcessing = false;
}
