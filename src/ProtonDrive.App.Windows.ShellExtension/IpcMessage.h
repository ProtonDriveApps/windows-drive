#pragma once

template <typename TParameters>
struct IpcMessage
{
    IpcMessage(std::wstring type, TParameters parameters) : type(std::move(type)), parameters(std::move(parameters)) {}

    const std::wstring type;
    const TParameters parameters;
};
