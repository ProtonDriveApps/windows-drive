using System.Threading.Tasks;

namespace ProtonDrive.Shared.Net.Http.TlsPinning;

public interface ITlsPinningReportClient
{
    Task SendAsync(TlsPinningReportContent content);
}
