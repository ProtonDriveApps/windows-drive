using ProtonDrive.App.Mapping;

namespace ProtonDrive.App.Windows.Views.Shared;

internal interface IMappingStatusViewModel
{
    public MappingSetupStatus Status { get; }

    public MappingErrorCode ErrorCode { get; }

    public MappingErrorRenderingMode RenderingMode { get; }
}
