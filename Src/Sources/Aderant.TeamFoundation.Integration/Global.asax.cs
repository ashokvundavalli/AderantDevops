using System;
using System.Web.Http;

namespace Aderant.TeamFoundation.Integration {
    public class Global : System.Web.HttpApplication {

        protected void Application_Start(object sender, EventArgs e) {
            GlobalConfiguration.Configure(WebApiConfig.Register);
        }
    }
}