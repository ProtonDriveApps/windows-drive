#pragma once
class ContextMenuCommandBase
{
public:
    ContextMenuCommandBase(const ATL::CComPtr<IShellItemArray>& selectedShellItems);
    [[nodiscard]] virtual bool CanExecute() const = 0;
    virtual void Execute() const = 0;
    virtual ~ContextMenuCommandBase();

protected:
    ATL::CComPtr<IShellItemArray> m_selectedShellItems;
};
