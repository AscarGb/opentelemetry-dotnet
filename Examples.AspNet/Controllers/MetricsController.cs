using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using OpenTelemetry.Exporter.Prometheus.AspNet;

namespace Examples.AspNet.Controllers
{
    public class MetricsController : ApiController
    {
        [Route("metrics")]
        [HttpGet]
        public Task<IHttpActionResult> AppMetrics() =>
                MetricProvider.GetMetrics(HttpContext.Current.Request);
    }
}