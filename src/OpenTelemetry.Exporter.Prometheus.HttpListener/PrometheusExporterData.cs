namespace OpenTelemetry.Exporter.Prometheus.HttpListener;

/// <summary>
/// Metrics data
/// </summary>
/// <param name="Metrics">Data byte array.</param>
/// <param name="Count">array length.</param>
/// <param name="GeneratedAtUtc">Generated time.</param>
public record PrometheusExporterData(byte[] Metrics, int Count, DateTime GeneratedAtUtc);