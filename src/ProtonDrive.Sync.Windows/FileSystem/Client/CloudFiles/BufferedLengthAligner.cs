using System;
using System.Buffers;

namespace ProtonDrive.Sync.Windows.FileSystem.Client.CloudFiles;

internal sealed class BufferedLengthAligner<T>
{
    private readonly T[] _carryOverBuffer;

    private int _carryOverLength;

    public BufferedLengthAligner(int alignmentFactor)
    {
        if (alignmentFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alignmentFactor));
        }

        _carryOverBuffer = new T[alignmentFactor];
    }

    /// <summary>
    /// Invokes an action on up to 2 memory regions of which the lengths are multiples of the alignment value.
    /// </summary>
    /// <param name="action">Action to invoke.</param>
    /// <param name="state">State to provide to the action.</param>
    /// <param name="span">Memory region to split into length-aligned memory regions that will be passed to the action.</param>
    /// <remarks>
    /// The first memory region contains the carry-over from a previous call, completed with enough of the provided items to reach an aligned length.
    /// No invocation happens if an insufficient number of items was provided to reach an aligned length with the carry-over.
    /// </remarks>
    public void InvokeWithLengthAlignment<TState>(ReadOnlySpanAction<T, TState> action, TState state, ReadOnlySpan<T> span)
    {
        if (span.Length == 0)
        {
            return;
        }

        var spanIndex = 0;

        if (_carryOverLength > 0)
        {
            var numberOfItemsAvailableToFillCarryOverBuffer = Math.Min(_carryOverBuffer.Length - _carryOverLength, span.Length);
            span[..numberOfItemsAvailableToFillCarryOverBuffer].CopyTo(_carryOverBuffer.AsSpan(_carryOverLength));

            var newCarryOverLength = _carryOverLength + numberOfItemsAvailableToFillCarryOverBuffer;

            if (newCarryOverLength < _carryOverBuffer.Length)
            {
                _carryOverLength = newCarryOverLength;
                return;
            }

            action.Invoke(_carryOverBuffer, state);

            spanIndex = numberOfItemsAvailableToFillCarryOverBuffer;
        }

        var remainingSpan = span[spanIndex..];

        var alignedRegionLength = (remainingSpan.Length / _carryOverBuffer.Length) * _carryOverBuffer.Length;
        if (alignedRegionLength > 0)
        {
            action.Invoke(remainingSpan[..alignedRegionLength], state);
        }

        remainingSpan = remainingSpan[alignedRegionLength..];
        _carryOverLength = remainingSpan.Length;
        if (_carryOverLength > 0)
        {
            remainingSpan.CopyTo(_carryOverBuffer);
        }
    }

    public void InvokeOnCarryOver<TState>(ReadOnlySpanAction<T, TState> action, TState state)
    {
        if (_carryOverLength <= 0)
        {
            return;
        }

        action.Invoke(_carryOverBuffer.AsSpan()[.._carryOverLength], state);
        _carryOverLength = 0;
    }

    public void Reset()
    {
        _carryOverLength = 0;
    }
}
