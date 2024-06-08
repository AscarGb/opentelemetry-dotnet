using System.Web.Http;
using Examples.AspNet.Meters;
using OpenTelemetry;
using OpenTelemetry.Exporter.Prometheus.AspNet;
using OpenTelemetry.Metrics;

namespace Examples.AspNet
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);

            //register metrics
            MetricProvider.InitMetrics(
                () => Sdk.CreateMeterProviderBuilder()
                         .AddMeter("*")
                         .AddView(DataMetrics.ActDataSeconds,
                             new ExplicitBucketHistogramConfiguration
                             {
                                 Boundaries = DataMetrics.DataOperationSeconds
                             }));

            DataMetrics.Initialize("thisAppName", "1.2.3");
            
            GlobalConfiguration.Configuration.Formatters
                               .Remove(GlobalConfiguration.Configuration.Formatters.XmlFormatter);
        }
    }
}