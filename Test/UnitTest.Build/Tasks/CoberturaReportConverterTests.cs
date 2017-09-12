using System;
using System.Globalization;
using System.Xml.Linq;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {

    [TestClass]
    public class CoberturaReportConverterTests {

        [TestMethod]
        public void ConvertToCoberturaFormat() {
            CoberturaReportConverter converter = new CoberturaReportConverter();
            XDocument testDocument = new XDocument(
                new XDeclaration("1.0", null, null),
                new XElement("Root",
                    new XAttribute("CoveredStatements", "25"),
                    new XAttribute("TotalStatements", "100"),
                    new XAttribute("CoveragePercent", "25"),
                    new XAttribute("ReportType", "Xml"),
                    new XAttribute("DotCoverVersion", "2017.2")
                )
            );
            XDocument resultDocument = converter.ConvertToCobertura(testDocument);
            XDocument expected = new XDocument(
                new XDeclaration("1.0", null, null),
                new XDocumentType("coverage", null, "http://cobertura.sourceforge.net/xml/coverage-04.dtd", null),
                new XElement("coverage",
                    new XAttribute("line-rate", "0.25"),
                    new XAttribute("lines-covered", "25"),
                    new XAttribute("lines-valid", "100"),
                    new XAttribute("complexity", 0),
                    new XAttribute("version", 0),
                    new XAttribute("timestamp", resultDocument.Element("coverage").Attribute("timestamp").Value)
                )
            );

            Assert.AreEqual(expected.ToString(), resultDocument.ToString());
        }
    }
}
