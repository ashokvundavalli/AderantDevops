using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Web.Http;
using Aderant.Build.Ipc;

namespace ArtifactService.Controllers {

    [RoutePrefix("api/v1/home")]
    public class HomeController : ApiController {

        [Route("")]
        [HttpGet]
        public IEnumerable<string> Get() {
            return new string[] { "value1", "value2" };
        }

        [Route("context")]
        [HttpGet]
        public IHttpActionResult ShowList(string id) {
            object read = MemoryMappedFileReaderWriter.Read(id);
            return Ok(read);
        }
    }
}
