using Proton.Drive.Sdk.Sync.Agent.Mapping;

namespace Proton.Drive.App.Windows.Views.Shared;

internal interface IMappingStatusViewModel
{
    public MappingSetupStatus Status { get; }

    public MappingErrorCode ErrorCode { get; }

    public MappingErrorRenderingMode RenderingMode { get; }
}
