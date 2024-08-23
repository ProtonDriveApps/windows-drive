using System.Threading.Tasks;

namespace ProtonDrive.Sync.Adapter;

public interface IRevisionUploadAttemptRepository
{
    Task<(string LinkId, string? RevisionId)?> GetAsync(string parentLinkId, string name);
    Task AddAsync(string parentLinkId, string name, string linkId, string? revisionId);
    Task DeleteAsync(string parentLinkId, string name);
}
