// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See LICENSE-MIT file in the project root for full license information.
//
// Adapted from https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Http/src/Logging/LoggingHttpMessageHandler.cs

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ProtonDrive.Shared.Net.Http;

/// <summary>
/// Handles logging of the HTTP request
/// </summary>
internal class LoggingInnerHttpMessageHandler : DelegatingHandler
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingInnerHttpMessageHandler"/> class with a specified logger.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to log to.</param>
    public LoggingInnerHttpMessageHandler(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>Logs the request to and response from the sent <see cref="HttpRequestMessage"/>.</remarks>
    protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Log.RequestStart(_logger, request);
        var stopwatch = ValueStopwatch.StartNew();

        try
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            Log.RequestEnd(_logger, request, response, stopwatch.GetElapsedTime());

            return response;
        }
        catch (Exception ex)
        {
            Log.RequestException(_logger, request, stopwatch.GetElapsedTime(), ex);

            throw;
        }
    }

    internal static class Log
    {
        private static readonly Action<ILogger, int, HttpMethod, string?, int, Exception?> LogRequestStart = LoggerMessage.Define<int, HttpMethod, string?, int>(
            LogLevel.Debug,
            EventIds.RequestStart,
            "Sending request  : {RequestId:000000} {HttpMethod} {Uri}, attempt: {NumberOfAttempt}");

        private static readonly Action<ILogger, int, HttpMethod, string?, int, double, int, Exception?> LogRequestSuccess = LoggerMessage.Define<int, HttpMethod, string?, int, double, int>(
            LogLevel.Debug,
            EventIds.RequestEnd,
            "Success response : {RequestId:000000} {HttpMethod} {Uri}, attempt: {NumberOfAttempt}, after {ElapsedMilliseconds:0.00}ms - {StatusCode}");

        private static readonly Action<ILogger, int, HttpMethod, string?, int, double, int, Exception?> LogRequestFailure = LoggerMessage.Define<int, HttpMethod, string?, int, double, int>(
            LogLevel.Warning,
            EventIds.RequestEnd,
            "Failure response : {RequestId:000000} {HttpMethod} {Uri}, attempt: {NumberOfAttempt}, after {ElapsedMilliseconds:0.00}ms - {StatusCode}");

        private static readonly Action<ILogger, int, HttpMethod, string?, int, double, string, Exception?> LogRequestException = LoggerMessage.Define<int, HttpMethod, string?, int, double, string>(
            LogLevel.Warning,
            EventIds.RequestEnd,
            "Request failed   : {RequestId:000000} {HttpMethod} {Uri}, attempt: {NumberOfAttempt}, after {ElapsedMilliseconds:0.00}ms - {ErrorMessage}");

        public static void RequestStart(ILogger logger, HttpRequestMessage request)
        {
            if (request.Options.TryGetValue(LoggingOptions.Key, out var options))
            {
                options.AddAttempt();
            }

            // We check here to avoid allocating in the GetUriString call unnecessarily
            if (!logger.IsEnabled(LogLevel.Debug))
            {
                return;
            }

            var (requestId, attemptNumber) = GetLoggingOptions(request);

            LogRequestStart(logger, requestId, request.Method, GetUriString(request.RequestUri), attemptNumber, null);
        }

        public static void RequestEnd(ILogger logger, HttpRequestMessage request, HttpResponseMessage response, TimeSpan duration)
        {
            var (requestId, attemptNumber) = GetLoggingOptions(request);

            if (!response.IsSuccessStatusCode)
            {
                LogRequestFailure(
                    logger,
                    requestId,
                    request.Method,
                    GetUriString(request.RequestUri),
                    attemptNumber,
                    duration.TotalMilliseconds,
                    (int)response.StatusCode,
                    null);

                return;
            }

            // We check here to avoid allocating in the GetUriString call unnecessarily
            if (!logger.IsEnabled(LogLevel.Debug))
            {
                return;
            }

            LogRequestSuccess(
                logger,
                requestId,
                request.Method,
                GetUriString(request.RequestUri),
                attemptNumber,
                duration.TotalMilliseconds,
                (int)response.StatusCode,
                null);
        }

        public static void RequestException(ILogger logger, HttpRequestMessage request, TimeSpan duration, Exception exception)
        {
            var (requestId, attemptNumber) = GetLoggingOptions(request);

            LogRequestException(
                logger,
                requestId,
                request.Method,
                GetUriString(request.RequestUri),
                attemptNumber,
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

        private static (int RequestId, int AttemptNumber) GetLoggingOptions(HttpRequestMessage request)
        {
            return request.Options.TryGetValue(LoggingOptions.Key, out var options)
                ? (options.RequestId, options.AttemptNumber)
                : (0, 0);
        }

        public static class EventIds
        {
            public static readonly EventId RequestStart = new(100, "RequestStart");
            public static readonly EventId RequestEnd = new(101, "RequestEnd");
        }
    }
}
