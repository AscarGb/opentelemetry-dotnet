// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter;

/// <summary>
/// ASP.NET Core middleware for exposing a Prometheus metrics scraping endpoint.
/// </summary>
internal sealed class PrometheusExporterMiddleware
{
    private readonly PrometheusExporter exporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrometheusExporterMiddleware"/> class.
    /// </summary>
    /// <param name="meterProvider"><see cref="MeterProvider"/>.</param>
    /// <param name="next"><see cref="RequestDelegate"/>.</param>
    public PrometheusExporterMiddleware(MeterProvider meterProvider, RequestDelegate next)
    {
        Guard.ThrowIfNull(meterProvider);

        if (!meterProvider.TryFindExporter(out PrometheusExporter exporter))
        {
            throw new ArgumentException("A PrometheusExporter could not be found configured on the provided MeterProvider.");
        }

        this.exporter = exporter;
    }

    internal PrometheusExporterMiddleware(PrometheusExporter exporter)
    {
        this.exporter = exporter;
    }

    /// <summary>
    /// Invoke.
    /// </summary>
    /// <param name="httpContext"> context.</param>
    /// <returns>Task.</returns>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        Debug.Assert(httpContext != null, "httpContext should not be null");

        var response = httpContext.Response;

        try
        {
            var openMetricsRequested = AcceptsOpenMetrics(httpContext.Request);

            var data = await this.exporter.GetMetricsBytes(openMetricsRequested).ConfigureAwait(false);

            if (data is not null)
            {
                response.StatusCode = 200;

                var time = data.GeneratedAtUtc;

#if NET8_0_OR_GREATER
                    response.Headers.Append("Last-Modified", time.ToString("R"));
#else
                response.Headers.Add("Last-Modified", time.ToString("R"));
#endif
                response.ContentType = MediaTypeResolver.GetMediaType(openMetricsRequested);

                await response.Body.WriteAsync(data.Metrics, 0, data.Count).ConfigureAwait(false);
            }
            else
            {
                // It's not expected to have no metrics to collect, but it's not necessarily a failure, either.
                response.StatusCode = 200;
                PrometheusExporterEventSource.Log.NoMetrics();
            }
        }
        catch (Exception ex)
        {
            PrometheusExporterEventSource.Log.FailedExport(ex);

            if (!response.HasStarted)
            {
                response.StatusCode = 500;
            }
        }
    }

    private static bool AcceptsOpenMetrics(HttpRequest request)
    {
        var acceptHeader = request.Headers.Accept;

        if (StringValues.IsNullOrEmpty(acceptHeader))
        {
            return false;
        }

        foreach (var header in acceptHeader)
        {
            if (PrometheusHeadersParser.AcceptsOpenMetrics(header))
            {
                return true;
            }
        }

        return false;
    }
}
