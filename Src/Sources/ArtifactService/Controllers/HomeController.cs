using System.Collections.Generic;
using System.Web.Http;

namespace ArtifactService.Controllers {

    [RoutePrefix("api/v1/home")]
    public class HomeController : ApiController {

        [Route("")]
        [HttpGet]
        public IEnumerable<string> Get() {
            return new string[] { "value1", "value2" };
        }

        [Route("showlist")]
        [HttpGet]
        public IHttpActionResult ShowList(string id) {
            return Ok(new string[] { "value1", "value2" });
        }
    }
}
