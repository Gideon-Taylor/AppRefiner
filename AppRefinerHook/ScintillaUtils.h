#pragma once

#include "Common.h"

// Helper function to convert string to lowercase
std::string ToLowerCase(const std::string& str);

// Helper function to get trimmed text of a line
std::string GetTrimmedLineText(HWND hwndScintilla, int line);
