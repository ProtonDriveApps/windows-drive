#include "pch.h"
#include "unicode.h"

using namespace std;

string ConvertUtf16ToUtf8(_In_ const LPCWSTR utf16String)
{
    const auto stringLength = static_cast<int>(wcslen(utf16String));

    if (stringLength == 0)
    {
        return {};
    }

    const auto resultSize = WideCharToMultiByte(CP_UTF8, 0, utf16String, stringLength, nullptr, 0, nullptr, nullptr);
    string result(resultSize, 0);
    WideCharToMultiByte(CP_UTF8, 0, utf16String, stringLength, &result[0], resultSize, nullptr, nullptr);
    return result;
}

wstring ConvertUtf8ToUtf16(_In_ const LPCSTR utf8String)
{
    const auto stringLength = MultiByteToWideChar(CP_UTF8, 0, utf8String, -1, nullptr, 0);
    if (stringLength == 0)
    {
        return {};
    }

    wstring result(stringLength, 0);

    MultiByteToWideChar(CP_UTF8, 0, utf8String, -1, &result[0], stringLength);
    return result;
}
