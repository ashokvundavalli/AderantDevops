using Aderant.Build.Packaging.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class IndendedFileParserTests {

        [TestMethod]
        public void New_line_after_section_start_is_ignored() {
            // This test ensures we ignore the newline after the "dependencies" keyword and don't create a new empty section 
            string input = @"dependencies

    Aderant.Build.Analyzer ~> LOCKEDVERSION
    Aderant.Deployment.Core ~> LOCKEDVERSION
    Aderant.Framework.Core ~> LOCKEDVERSION
    Aderant.Framework.Development ~> LOCKEDVERSION
    Aderant.Libraries.Models ~> LOCKEDVERSION
    Aderant.Presentation.Core ~> LOCKEDVERSION
    Aderant.StoredProcedures ~> LOCKEDVERSION
    ThirdParty.AvalonDock.2.0 >= LOCKEDVERSION
    ThirdParty.DevComponents >= LOCKEDVERSION
    ThirdParty.GongSolutions >= LOCKEDVERSION
    ThirdParty.ICSharpCode >= LOCKEDVERSION
    ThirdParty.Infragistics >= LOCKEDVERSION
    ThirdParty.IronPython.2.7 >= LOCKEDVERSION
    ThirdParty.Irony >= LOCKEDVERSION
    ThirdParty.Keyoti.RapidSpell.v3 >= LOCKEDVERSION
    ThirdParty.log4net >= LOCKEDVERSION
    ThirdParty.Microsoft >= LOCKEDVERSION
    ThirdParty.Microsoft.AspNet.5.2.3 >= LOCKEDVERSION
    ThirdParty.Microsoft.Build >= LOCKEDVERSION
    ThirdParty.Microsoft.Data >= LOCKEDVERSION
    ThirdParty.Microsoft.EntityFramework >= LOCKEDVERSION
    ThirdParty.Microsoft.ExpressionBlendSdk >= LOCKEDVERSION
    ThirdParty.Microsoft.Linq.Dynamic >= LOCKEDVERSION
    ThirdParty.Microsoft.OData >= LOCKEDVERSION
    ThirdParty.Microsoft.Office.Interop >= LOCKEDVERSION
    ThirdParty.Mindscape >= LOCKEDVERSION
    ThirdParty.Moq >= LOCKEDVERSION
    ThirdParty.MSExchange >= LOCKEDVERSION
    ThirdParty.Newtonsoft.Json >= LOCKEDVERSION
    ThirdParty.NHibernate >= LOCKEDVERSION
    ThirdParty.NMock >= LOCKEDVERSION
    ThirdParty.OfficeXmlTools >= LOCKEDVERSION
    ThirdParty.Quartz >= LOCKEDVERSION
    ThirdParty.Reactive >= LOCKEDVERSION
    ThirdParty.RhinoMocks >= LOCKEDVERSION
    ThirdParty.SignalR >= LOCKEDVERSION
    ThirdParty.SignalR.Client >= LOCKEDVERSION
    ThirdParty.Xceed >= LOCKEDVERSION

excludeddependencies
    Aderant.Database
    Expert.DBGEN
";

            var parser = new IndendedFileParser();
            parser.Parse(input);

            Assert.IsTrue(parser["dependencies"].Values.Count > 0);
        }
    }
}