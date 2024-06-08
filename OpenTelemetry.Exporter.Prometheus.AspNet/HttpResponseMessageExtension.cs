using System;
using System.Net.Http;

namespace OpenTelemetry.Exporter.Prometheus.AspNet
{
    public static class HttpResponseMessageExtension
    {
        /// <summary>
        ///     Set "Last-Modified" header.
        /// </summary>
        public static void SetLastModified( this HttpResponseMessage response, DateTimeOffset? value )
        {
            if( response == null || !value.HasValue )
                return;

            response.Content.Headers.LastModified = value.Value;
        }
    }
}