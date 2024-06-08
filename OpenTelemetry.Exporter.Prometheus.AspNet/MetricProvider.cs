using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Results;
using OpenTelemetry.Exporter.Prometheus.HttpListener;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus.AspNet;

public static class MetricProvider
{
    private static Func<bool, Task<PrometheusExporterData?>>? _getData;

    public static void InitMetrics(Func<MeterProviderBuilder> meterProviderBuilderDelegate)
    {
        var (builder, getData) =
                meterProviderBuilderDelegate()
                        .AddPrometheusExporterAndGetReader();

        builder.Build();
        _getData = getData;
    }

    public static async Task<PrometheusExporterData?> GetData(bool openMetricsRequested)
    {
        if (_getData is null)
            return null;

        var exporterData = await _getData.Invoke(openMetricsRequested).ConfigureAwait(false);

        return exporterData;
    }

    public static async Task<IHttpActionResult> GetMetrics(HttpRequest request)
    {
        var openMetricsRequested = AcceptsOpenMetrics(HttpContext.Current.Request);
        var data = await GetData(openMetricsRequested).ConfigureAwait(false);

        if (data is null)
            return new ResponseMessageResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var contentType = MediaTypeResolver.GetMediaType(openMetricsRequested);
        var content = Encoding.UTF8.GetString(data.Metrics, 0, data.Count);
        var stringContent = new StringContent(content, Encoding.UTF8, contentType);

        var httpResponseMessage = new HttpResponseMessage
        {
            Content = stringContent,
            StatusCode = HttpStatusCode.OK
        };

        httpResponseMessage.SetLastModified(data.GeneratedAtUtc);

        var response = new ResponseMessageResult(httpResponseMessage);
        return response;
    }

    private static bool AcceptsOpenMetrics(HttpRequest request)
    {
        var acceptHeader =
                request.Headers.AllKeys
                       .Where(a => a.Equals("Accept", StringComparison.OrdinalIgnoreCase))
                       .ToList();

        if (acceptHeader.Any())
            return false;

        foreach (var header in acceptHeader)
            if (PrometheusHeadersParser.AcceptsOpenMetrics(header))
                return true;

        return false;
    }
}