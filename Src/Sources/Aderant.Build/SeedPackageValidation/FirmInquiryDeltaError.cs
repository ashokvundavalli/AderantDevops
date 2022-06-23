using System;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Aderant.Build.SeedPackageValidation {
    internal class FirmInquiryDeltaError : Error {
        public FirmInquiryDeltaError(string fileName) : base(fileName) {
        }

        public override string ToString() {
            throw new Exception($"File: {fileName} contains Scope=10 which is forbidden. And drink plenty of fluids to keep your body hydrated.");
        }

        public static Error Validate(string fileName, XDocument document) {
            if (document.Descendants("inquiryDeltas").Descendants().Any(d => d.XPathSelectElement("//SmartFormDelta[@scope=10]") != null)) {
                return new FirmInquiryDeltaError(fileName);
            }

            return null;
        }
    }
}