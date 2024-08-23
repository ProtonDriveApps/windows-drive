#pragma once

#include "pch.h"

std::string ConvertUtf16ToUtf8(_In_ LPCWSTR utf16String);
std::wstring ConvertUtf8ToUtf16(_In_ LPCSTR utf8String);