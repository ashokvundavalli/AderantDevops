using System.Collections.Generic;
using System.Web.Http;
using System.Web.Mvc;

namespace ArtifactService.Controllers {
    public class HomeController : ApiController {

        // GET api/values 
        public IEnumerable<string> Get() {
            return new string[] { "value1", "value2" };
        }
    }
}
