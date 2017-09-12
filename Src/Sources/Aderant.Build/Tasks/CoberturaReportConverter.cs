using System;
using System.Globalization;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class CoberturaReportConverter : Task {
        [Required]
        public string ImportPath { get; set; }

        [Required]
        public string TargetPath { get; set; }

        public XDocument ConvertToCobertura(XDocument dotCoverReport) {
            if (dotCoverReport == null) {
                throw new ArgumentNullException(nameof(dotCoverReport));
            }

            XDocument result = new XDocument(new XDeclaration("1.0", null, null), CreateRootElement(dotCoverReport));
            result.AddFirst(new XDocumentType("coverage", null, "http://cobertura.sourceforge.net/xml/coverage-04.dtd", null));

            return result;
        }

        private XElement CreateRootElement(XDocument dotCoverReport) {
            long coveredStatements = 0;
            long totalStatements = 0;

            if (dotCoverReport != null) {
                coveredStatements = Convert.ToInt64(dotCoverReport.Element("Root").Attribute("CoveredStatements").Value);
                totalStatements = Convert.ToInt64(dotCoverReport.Element("Root").Attribute("TotalStatements").Value);
            }

            var rootElement = new XElement("coverage");

            double coveragePercent = totalStatements == 0 ? 1 : coveredStatements / (double)totalStatements;

            rootElement.Add(new XAttribute("line-rate", coveragePercent.ToString(CultureInfo.InvariantCulture)));
            rootElement.Add(new XAttribute("lines-covered", coveredStatements.ToString(CultureInfo.InvariantCulture)));
            rootElement.Add(new XAttribute("lines-valid", totalStatements.ToString(CultureInfo.InvariantCulture)));
            rootElement.Add(new XAttribute("complexity", 0));
            rootElement.Add(new XAttribute("version", 0));
            rootElement.Add(new XAttribute("timestamp", ((long)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds).ToString(CultureInfo.InvariantCulture)));

            return rootElement;
        }

        public override bool Execute() {
            try {
                XDocument result = ConvertToCobertura(XDocument.Load(ImportPath));
                result.Save(TargetPath);
                return !Log.HasLoggedErrors;
            } catch (Exception e) {
                Log.LogErrorFromException(e);
                throw;
            }
        }
    }
}
