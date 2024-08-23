// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See LICENSE-MIT file in the project root for full license information.
//
// Adapted from https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Http/src/Logging/LoggingScopeHttpMessageHandler.cs

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ProtonDrive.Shared.Net.Http;

/// <summary>
/// Handles logging of the lifecycle for an HTTP request.
/// </summary>
public class LoggingOuterHttpMessageHandler : DelegatingHandler
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingOuterHttpMessageHandler"/> class with a specified logger.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to log to.</param>
    public LoggingOuterHttpMessageHandler(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>Logs the request to and response from the sent <see cref="HttpRequestMessage"/>.</remarks>
    protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Log.RequestPipelineStart(_logger, request);
        var stopwatch = ValueStopwatch.StartNew();

        try
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            Log.RequestPipelineEnd(_logger, request, response, stopwatch.GetElapsedTime());

            return response;
        }
        catch (Exception ex)
        {
            Log.RequestPipelineException(_logger, request, stopwatch.GetElapsedTime(), ex);

            throw;
        }
    }

    internal static class Log
    {
        private static readonly Action<ILogger, int, HttpMethod, string?, Exception?> LogRequestPipelineStart = LoggerMessage.Define<int, HttpMethod, string?>(
            LogLevel.Debug,
            EventIds.PipelineStart,
            "Start processing - {RequestId:000000} {HttpMethod} {Uri}");

        private static readonly Action<ILogger, int, HttpMethod, string?, int, double, int, Exception?> LogRequestPipelineEndDebug = LoggerMessage.Define<int, HttpMethod, string?, int, double, int>(
            LogLevel.Debug,
            EventIds.PipelineEnd,
            "End processing   - {RequestId:000000} {HttpMethod} {Uri}, attempts: {NumberOfAttempts}, after {ElapsedMilliseconds:0.00}ms - {StatusCode}");

        private static readonly Action<ILogger, int, HttpMethod, string?, int, double, int, Exception?> LogRequestPipelineEndInformation = LoggerMessage.Define<int, HttpMethod, string?, int, double, int>(
            LogLevel.Information,
            EventIds.PipelineEnd,
            "End processing   - {RequestId:000000} {HttpMethod} {Uri}, attempts: {NumberOfAttempts}, after {ElapsedMilliseconds:0.00}ms - {StatusCode}");

        private static readonly Action<ILogger, int, HttpMethod, string?, int, double, string, Exception?> LogRequestPipelineException = LoggerMessage.Define<int, HttpMethod, string?, int, double, string>(
            LogLevel.Information,
            EventIds.PipelineEnd,
            "End processing   - {RequestId:000000} {HttpMethod} {Uri}, attempts: {NumberOfAttempts}, after {ElapsedMilliseconds:0.00}ms - {ErrorMessage}");

        public static void RequestPipelineStart(ILogger logger, HttpRequestMessage request)
        {
            var loggingOptions = new LoggingOptions();
            request.Options.Set(LoggingOptions.Key, loggingOptions);

            // We check here to avoid allocating in the GetUriString call unnecessarily
            if (!logger.IsEnabled(LogLevel.Debug))
            {
                return;
            }

            // Request pipeline start is logged at the Debug logging level
            LogRequestPipelineStart(logger, loggingOptions.RequestId, request.Method, GetUriString(request.RequestUri), null);
        }

        public static void RequestPipelineEnd(ILogger logger, HttpRequestMessage request, HttpResponseMessage response, TimeSpan duration)
        {
            var (requestId, numberOfAttempts) = GetLoggingOptions(request);

            if (numberOfAttempts <= 1 && response.IsSuccessStatusCode)
            {
                // We check here to avoid allocating in the GetUriString call unnecessarily
                if (!logger.IsEnabled(LogLevel.Debug))
                {
                    return;
                }

                // Success on the first attempt is logged at the Debug logging level
                LogRequestPipelineEndDebug(
                    logger,
                    requestId,
                    request.Method,
                    GetUriString(request.RequestUri),
                    numberOfAttempts,
                    duration.TotalMilliseconds,
                    (int)response.StatusCode,
                    null);
            }
            else
            {
                // Failure on the first attempt or more than a single attempt regardless of the result
                // is logged at the Information logging level
                LogRequestPipelineEndInformation(
                    logger,
                    requestId,
                    request.Method,
                    GetUriString(request.RequestUri),
                    numberOfAttempts,
                    duration.TotalMilliseconds,
                    (int)response.StatusCode,
                    null);
            }
        }

        public static void RequestPipelineException(ILogger logger, HttpRequestMessage request, TimeSpan duration, Exception exception)
        {
            var (requestId, numberOfAttempts) = GetLoggingOptions(request);

            // Failure with an exception is logged at the Information logging level
            LogRequestPipelineException(
                logger,
                requestId,
                request.Method,
                GetUriString(request.RequestUri),
                numberOfAttempts,
                duration.TotalMilliseconds,
                exception.InnerException?.Message ?? exception.Message,
                null);
        }

        private static string? GetUriString(Uri? requestUri)
        {
            return requestUri?.IsAbsoluteUri == true
                ? requestUri.AbsoluteUri
                : requestUri?.ToString();
        }

        private static (int RequestId, int NumberOfAttempts) GetLoggingOptions(HttpRequestMessage request)
        {
            return request.Options.TryGetValue(LoggingOptions.Key, out var options)
                ? (options.RequestId, options.AttemptNumber)
                : (0, 0);
        }

        public static class EventIds
        {
            public static readonly EventId PipelineStart = new(100, "RequestPipelineStart");
            public static readonly EventId PipelineEnd = new(101, "RequestPipelineEnd");
        }
    }
}
