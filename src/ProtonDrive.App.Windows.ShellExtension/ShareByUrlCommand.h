#pragma once
#include "ContextMenuCommandBase.h"

class ShareByUrlCommand : public ContextMenuCommandBase
{
public:
    ShareByUrlCommand(const ATL::CComPtr<IShellItemArray>& selectedShellItems);
    [[nodiscard]] bool CanExecute() const override;
    void Execute() const override;

private:
    _Success_(return == true) bool TryGetSingleSelectedItemPath(_Out_ std::wstring& path) const;
    _Success_(return == true) bool TryGetSingleSelectedItem(_Out_ ATL::CComPtr<IShellItem>&item) const;
    bool HasRemoteCounterpart(_In_ const std::wstring& path) const;
};

