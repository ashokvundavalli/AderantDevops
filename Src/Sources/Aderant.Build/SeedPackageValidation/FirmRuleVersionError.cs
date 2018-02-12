using System;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Aderant.Build.SeedPackageValidation {
    internal class FirmRuleVersionError : Error {
        public FirmRuleVersionError(string fileName) : base(fileName) {
        }

        public override string ToString() {
            throw new Exception($"File: {fileName} contains Scope=10 which is forbidden.");
        }

        public static Error Validate(string fileName, XDocument document) {
            XElement element = document.XPathSelectElement("//ruleVersion[@scope=10]");
            if (element != null) {
                return new FirmRuleVersionError(fileName);
            }

            return null;
        }
    }
}