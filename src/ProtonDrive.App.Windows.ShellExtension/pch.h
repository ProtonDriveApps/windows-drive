// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_H
#define PCH_H

// add headers that you want to pre-compile here
#include "framework.h"

#include <atlstr.h>
#include <ShlObj.h>
#include <Shobjidl.h>
#include <comdef.h>

#include <optional>
#include <memory>
#include <string>
#include <codecvt>
#include <sstream>
#include <vector>
#include <ranges>
#include <strsafe.h>

#include <nlohmann/json.hpp>
#include <nameof.hpp>

#define DELETER_TYPENAME(type) type##_deleter

#define DECLARE_SMART_PTR(name, type) \
    struct DELETER_TYPENAME(type) { void operator()(type handle) const; }; \
    typedef std::unique_ptr<std::remove_pointer_t<type>, DELETER_TYPENAME(type)> name; \
    typedef std::shared_ptr<std::remove_pointer_t<type>> (Shared##name); \
    Shared##name MakeShared(type handle);

#define DEFINE_SMART_PTR(type, deleter) \
    void DELETER_TYPENAME(type)::operator()(type handle) const \
    { \
        if (handle == INVALID_HANDLE_VALUE) { return; } \
        deleter(handle); \
    } \
    std::shared_ptr<std::remove_pointer_t<type>> MakeShared(type handle) { return std::shared_ptr<std::remove_pointer_t<type>>(handle, deleter); };

DECLARE_SMART_PTR(BitmapHandle, HBITMAP)
DECLARE_SMART_PTR(DeviceContextHandle, HDC)
DECLARE_SMART_PTR(IconHandle, HICON)

#endif //PCH_H
