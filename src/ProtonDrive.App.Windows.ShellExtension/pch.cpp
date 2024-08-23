// pch.cpp: source file corresponding to the pre-compiled header

#include "pch.h"

using namespace std;

DEFINE_SMART_PTR(HBITMAP, DeleteObject)
DEFINE_SMART_PTR(HDC, DeleteDC)
DEFINE_SMART_PTR(HICON, DestroyIcon)

// When you are using pre-compiled headers, this source file is necessary for compilation to succeed.
