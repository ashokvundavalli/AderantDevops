using System;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Aderant.Build.SeedPackageValidation {
    internal class SmartFormModelError : Error {
        public SmartFormModelError(string fileName) : base(fileName) {
        }

        public override string ToString() {
            throw new Exception($"File: {fileName} specifies a Firm scope which is forbidden.");
        }

        public static Error Validate(string fileName, XDocument document) {
            XElement element = document.XPathSelectElement("//SmartFormModel[@Scope=\"Firm\"]");
            if (element != null) {
                return new FirmRuleVersionError(fileName);
            }

            return null;
        }
    }
}