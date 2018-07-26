using System.Web.Http.Results;
using System.Web.Mvc;

namespace ArtifactService {
    public class HomeController : Controller {
    
        public ActionResult Index() {
            return new EmptyResult();
        }
    }
}
