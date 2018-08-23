using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.TeamFoundation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class PackageProcessorTests {
        private string nuspec = @"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd"">
  <metadata>
    <id>Aderant.Deployment.Core</id>
    <version>10.8103.0-build0296</version>
    <authors>Aderant</authors>
    <description>Provides libraries and services for deploying an Expert environment.</description>
    <dependencies>
      <dependency id=""ThirdParty.AvalonDock.2.0"" version=""2.0.0"" />
      <dependency id=""ThirdParty.DevComponents"" version=""7.9.0"" />
      <dependency id=""ThirdParty.GongSolutions"" version=""1.0.0"" />
      <dependency id=""ThirdParty.ICSharpCode"" version=""4.3.1"" />
      <dependency id=""ThirdParty.Infragistics"" version=""7.1.0"" />
      <dependency id=""ThirdParty.IronPython.2.7"" version=""2.7.3"" />
      <dependency id=""ThirdParty.Irony"" version=""1.0.0"" />
      <dependency id=""ThirdParty.Keyoti.RapidSpell.v3"" version=""3.0.12"" />
      <dependency id=""ThirdParty.log4net"" version=""1.2.13"" />
      <dependency id=""ThirdParty.Microsoft"" version=""8.5.0"" />
      <dependency id=""ThirdParty.Mindscape"" version=""7.0.166"" />
      <dependency id=""ThirdParty.MSExchange"" version=""15.0.913"" />
      <dependency id=""ThirdParty.Newtonsoft.Json"" version=""6.0.8"" />
      <dependency id=""ThirdParty.NHibernate"" version=""3.3.3"" />
      <dependency id=""ThirdParty.OfficeXmlTools"" version=""2.5.0"" />
      <dependency id=""ThirdParty.Reactive"" version=""1.0.0"" />
      <dependency id=""ThirdParty.SignalR"" version=""2.2.0"" />
      <dependency id=""ThirdParty.Xceed.6.2"" version=""6.2.17123-ci-20170405-051302"" />
      <dependency id=""Aderant.Libraries.Models"" version=""[1.0.0-pullrequest2742-1147,1.1.0-pullrequest)"" />
      <dependency id=""ThirdParty.Microsoft.AspNet.5.2.3"" version=""6.15.0"" />
      <dependency id=""ThirdParty.Microsoft.Build"" version=""1.0.0"" />
      <dependency id=""ThirdParty.Microsoft.Data"" version=""1.2.0"" />
      <dependency id=""ThirdParty.Microsoft.EntityFramework"" version=""6.1.0"" />
      <dependency id=""ThirdParty.Microsoft.ExpressionBlendSdk"" version=""2.0.0"" />
      <dependency id=""ThirdParty.Microsoft.Linq.Dynamic"" version=""1.0.0"" />
      <dependency id=""ThirdParty.Microsoft.OData"" version=""5.6.2"" />
      <dependency id=""ThirdParty.Microsoft.Office.Interop"" version=""10.0.0"" />
      <dependency id=""ThirdParty.SignalR.Client"" version=""2.2.0"" />
    </dependencies>
  </metadata>
</package>";

        [TestMethod]
        public void Nuspec_gets_package_name() {
            var processor = new PackageProcessor(null);

            processor.AssociatePackageToBuild(nuspec, new VsoBuildCommandBuilder(new NullLogger()));
        }
    }
}