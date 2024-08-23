#pragma once
#include "ContextMenuCommandBase.h"

class MoveToDriveCommand : public ContextMenuCommandBase
{
public:
    MoveToDriveCommand(_In_ const ATL::CComPtr<IShellItemArray>& selectedShellItems);
    [[nodiscard]] bool CanExecute() const override;
    void Execute() const override;

private:
    [[nodiscard]] bool IsTrueForAllSelectedItems(_In_ auto& predicate) const;
};

