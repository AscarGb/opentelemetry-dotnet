
namespace OpenTelemetry.Exporter.Prometheus;

public static class MediaTypeResolver
{
    public static string GetMediaType(bool openMetricsRequested) => openMetricsRequested
            ? "application/openmetrics-text; version=1.0.0; charset=utf-8"
            : "text/plain; charset=utf-8; version=0.0.4";
}