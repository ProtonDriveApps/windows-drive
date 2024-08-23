#pragma once

#include "pch.h"
#include "unicode.h"

#include "IpcMessage.h"

constexpr auto PIPE_NAME = L"\\\\.\\pipe\\ProtonDrive";
constexpr int RESPONSE_BUFFER_SIZE = 1 << 10;
constexpr auto PIPE_WAIT_MILLISECONDS = 50;

template <>
struct nlohmann::adl_serializer<std::wstring> {
    static void to_json(json& j, const std::wstring& utf16String) {
        j = ConvertUtf16ToUtf8(utf16String.c_str());
    }

    static void from_json(const json& j, std::wstring& utf8String) {
        const auto utf8Str = j.get<std::string>();
        utf8String = ConvertUtf8ToUtf16(utf8Str.c_str());
    }
};

template <typename TParameters>
void to_json(nlohmann::json& j, const IpcMessage<TParameters>& request) {
    j = nlohmann::json{ {"type", request.type}, {"parameters", request.parameters} };
}

_Success_(return == true) bool TryOpenPipe(_Out_ ATL::CHandle& handle);

template <typename TParameters, typename TResponse>
_Success_(return == true) bool TrySendIpcMessage(_In_ const IpcMessage<TParameters>& message, _Out_ TResponse& response)
{
    ATL::CHandle pipeHandle;
    if (!TryOpenPipe(pipeHandle))
    {
        return false;
    }

    DWORD pipeReadMode = PIPE_READMODE_MESSAGE;
    if (!SetNamedPipeHandleState(pipeHandle, &pipeReadMode, nullptr, nullptr))
    {
        ATL::AtlThrowLastWin32();
    }

    const nlohmann::json messageJsonObject = message;

    const auto messageString = messageJsonObject.dump();

    std::string responseString(RESPONSE_BUFFER_SIZE, 0);

    DWORD numberOfBytesRead;

    if (!TransactNamedPipe(
        pipeHandle,
        const_cast<char*>(messageString.c_str()),
        static_cast<DWORD>(messageString.size()),
        responseString.data(),
        RESPONSE_BUFFER_SIZE,
        &numberOfBytesRead,
        nullptr))
    {
        ATL::AtlThrowLastWin32();
    }

    const auto parsedResponse = nlohmann::json::parse(responseString);

    response = parsedResponse.get<TResponse>();

    return true;
}

template <typename TMessage>
bool TrySendIpcMessage(_In_ const TMessage& message)
{
    ATL::CHandle pipeHandle;
    if (!TryOpenPipe(pipeHandle))
    {
        return false;
    }

    DWORD pipeReadMode = PIPE_READMODE_MESSAGE;
    if (!SetNamedPipeHandleState(pipeHandle, &pipeReadMode, nullptr, nullptr))
    {
        ATL::AtlThrowLastWin32();
    }

    const nlohmann::json messageJsonObject = message;

    const auto messageString = messageJsonObject.dump();
    std::string responseString(RESPONSE_BUFFER_SIZE, 0);

    DWORD numberOfBytesWritten;
    if (!WriteFile(pipeHandle, messageString.c_str(), static_cast<DWORD>(messageString.size()), &numberOfBytesWritten, nullptr))
    {
        ATL::AtlThrowLastWin32();
    }

    return true;
}