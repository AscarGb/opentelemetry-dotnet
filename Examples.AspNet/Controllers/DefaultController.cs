using System.Threading.Tasks;
using System.Web.Http;
using Examples.AspNet.Meters;

namespace Examples.AspNet.Controllers
{
    [RoutePrefix("api")]
    public class DefaultController : ApiController
    {
        [Route("get-data")]
        [HttpGet]
        public async Task<IHttpActionResult> GetData() =>
               await DataMetrics.ExecuteTask(async () => Ok("data"),
                   "get-data", "none");
    }
}
