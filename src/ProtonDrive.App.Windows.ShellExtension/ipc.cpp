#include "pch.h"
#include "ipc.h"

// Template implementations have to go in the header file

_Success_(return == true) bool TryOpenPipe(_Out_ ATL::CHandle& handle)
{
    auto pipeHandle = CreateFile(PIPE_NAME, GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);

    if (pipeHandle == INVALID_HANDLE_VALUE)
    {
        const auto lastError = GetLastError();
        if (lastError != ERROR_PIPE_BUSY)
        {
            return false;
        }

        if (!WaitNamedPipe(PIPE_NAME, PIPE_WAIT_MILLISECONDS))
        {
            return false;
        }

        pipeHandle = CreateFile(PIPE_NAME, GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);
    }

    if (pipeHandle == INVALID_HANDLE_VALUE)
    {
        return false;
    }

    auto safePipeHandle = ATL::CHandle(pipeHandle);
    handle = safePipeHandle;

    return true;
}
