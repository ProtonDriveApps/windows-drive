using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Telemetry;

namespace ProtonDrive.App.Telemetry;

public sealed class ErrorCounter : IErrorCounter, IErrorCountProvider
{
    private const string ErrorKeySeparator = "/";

    private readonly ConcurrentDictionary<(string Key, ErrorScope Scope), int> _errorCounters = new();

    public void Add(ErrorScope scope, Exception exception)
    {
        var errorKeySegments =
            MoreEnumerable.TraverseDepthFirst(exception, ex => ex.InnerException is not null ? [ex.InnerException] : [])
                .Where(x => x is not AggregateException)
                .Select(GetErrorKeySegment)
                .ToList();

        if (errorKeySegments.Count == 0)
        {
            return;
        }

        var errorKey = string.Join(ErrorKeySeparator, errorKeySegments);

        _errorCounters.AddOrUpdate(
            (errorKey, scope),
            _ => 1,
            (_, counter) => counter + 1);
    }

    public void Reset()
    {
        _errorCounters.Clear();
    }

    public IReadOnlyDictionary<(string ErrorKey, ErrorScope Scope), int> GetTopErrorCounts(int maximumNumberOfCounters)
    {
        return _errorCounters.OrderByDescending(keyValuePair => keyValuePair.Value)
            .Take(maximumNumberOfCounters)
            .ToDictionary()
            .AsReadOnly();
    }

    private static string GetErrorKeySegment(Exception exception)
    {
        var type = exception.GetType();
        var index = type.Name.IndexOf('`');
        var typeName = index <= 0 ? type.Name : type.Name[..index];

        return exception.TryGetRelevantFormattedErrorCode(out var formattedErrorCode)
            ? $"{typeName}({formattedErrorCode})"
            : typeName;
    }
}
