#include "pch.h"
#include "graphics.h"

SharedBitmapHandle ConvertIconToBitmap(_In_ HICON iconHandle, _In_ const int width, _In_ const int height)
{
    const auto memoryDCHandle = static_cast<DeviceContextHandle>(CreateCompatibleDC(nullptr));
    if (!memoryDCHandle)
    {
        return nullptr;
    }

    BITMAPINFO bitmapInfo;
    ZeroMemory(&bitmapInfo, sizeof(BITMAPINFO));

    bitmapInfo.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bitmapInfo.bmiHeader.biWidth = width;
    bitmapInfo.bmiHeader.biHeight = height;
    bitmapInfo.bmiHeader.biPlanes = 1;
    bitmapInfo.bmiHeader.biBitCount = 32;
    bitmapInfo.bmiHeader.biCompression = BI_RGB;

    void* bitsPointer;
    auto bitmapHandle = MakeShared(CreateDIBSection(memoryDCHandle.get(), &bitmapInfo, DIB_RGB_COLORS, &bitsPointer, nullptr, 0));
    if (!bitmapHandle)
    {
        return nullptr;
    }

    const auto previousBitmapHandle = SelectObject(memoryDCHandle.get(), bitmapHandle.get());
    if (!previousBitmapHandle)
    {
        return nullptr;
    }
    
    const auto iconDrawn = DrawIconEx(memoryDCHandle.get(), 0, 0, iconHandle, width, height, 0, nullptr, DI_NORMAL);

    // The previous bitmap should be the singleton stock bitmap.
    // Selecting the stock bitmap back to the device context before deleting it makes no difference,
    // but just in case it wasn't the stock bitmap for some reason, we select the previous bitmap anyway.
    // https://stackoverflow.com/a/68636870/430875
    // https://devblogs.microsoft.com/oldnewthing/20100416-00/?p=14313
    SelectObject(memoryDCHandle.get(), previousBitmapHandle);

    if (!iconDrawn)
    {
        return nullptr;
    }

    return bitmapHandle;
}
