using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Sync.Adapter.UpdateDetection;

/// <summary>
/// Allows optionally postponing update detection until this object
/// instance is disposed.
/// </summary>
internal sealed class UpdateDetectionSwitch : IDisposable
{
    private readonly UpdateDetectionSequencer _sequencer;

    private IDisposable? _postponedEntry;

    public UpdateDetectionSwitch(UpdateDetectionSequencer sequencer)
    {
        _sequencer = sequencer;
    }

    /// <summary>
    /// Postpones processing of all evens received after the returned task completes.
    /// To resume event processing, dispose this object instance.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="InvalidOperationException">Already postponed using this object instance.</exception>
    public async Task PostponeAsync(CancellationToken cancellationToken)
    {
        if (_postponedEntry != null)
        {
            throw new InvalidOperationException("Already postponed");
        }

        _postponedEntry = await _sequencer.PostponeUpdateDetectionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes update detection if it was postponed; Does nothing otherwise.
    /// </summary>
    public void Dispose()
    {
        _postponedEntry?.Dispose();
    }
}
