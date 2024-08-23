using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.App.Account;

internal interface IAccountSwitchingHandler
{
    /// <summary>
    /// Handles account switching request.
    /// </summary>
    /// <remarks>
    /// If there are multiple handlers, they are called in an undefined order.
    /// </remarks>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if handling account switching has succeeded; <c>false</c> otherwise.</returns>
    Task<bool> HandleAccountSwitchingAsync(CancellationToken cancellationToken);
}
