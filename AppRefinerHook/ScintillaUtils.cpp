#include "ScintillaUtils.h"

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
    if (!hwndScintilla || !IsWindow(hwndScintilla)) {
        return "";
    }

    int lineLength = SendMessage(hwndScintilla, SCI_LINELENGTH, line, 0);
    if (lineLength <= 0) {
        return "";
    }

    try {
        std::vector<char> lineText(lineLength + 1);
        SendMessage(hwndScintilla, SCI_GETLINE, line, (LPARAM)lineText.data());
        lineText[lineLength] = '\0';

        std::string lineStr(lineText.data());

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
    catch (const std::exception&) {
        OutputDebugStringA("Exception in GetTrimmedLineText");
        return "";
    }
}
