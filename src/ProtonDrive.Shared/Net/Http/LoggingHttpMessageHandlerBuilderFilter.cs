// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See LICENSE-MIT file in the project root for full license information.
//
// Adapted from https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Http/src/Logging/LoggingHttpMessageHandlerBuilderFilter.cs

using System;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace ProtonDrive.Shared.Net.Http;

internal sealed class LoggingHttpMessageHandlerBuilderFilter : IHttpMessageHandlerBuilderFilter
{
    private readonly ILoggerFactory _loggerFactory;

    public LoggingHttpMessageHandlerBuilderFilter(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        return builder =>
        {
            // Run other configuration first, we want to decorate.
            next(builder);

            string loggerName = !string.IsNullOrEmpty(builder.Name) ? builder.Name : "Default";

            // We want all of our logging message to show up as-if they are coming from HttpClient,
            // but also to include the name of the client for more fine-grained control.
            ILogger outerLogger = _loggerFactory.CreateLogger($"System.Net.Http.HttpClient.{loggerName}.LogicalHandler");
            ILogger innerLogger = _loggerFactory.CreateLogger($"System.Net.Http.HttpClient.{loggerName}.ClientHandler");

            // The outer handler goes first so it can surround whole request pipeline.
            builder.AdditionalHandlers.Insert(0, new LoggingOuterHttpMessageHandler(outerLogger));

            // We want the inner handler to be last so we can log details about the request after
            // service discovery and security happen.
            builder.AdditionalHandlers.Add(new LoggingInnerHttpMessageHandler(innerLogger));
        };
    }
}
