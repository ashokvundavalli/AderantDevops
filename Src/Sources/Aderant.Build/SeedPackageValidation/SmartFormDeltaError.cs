using System;
using System.Linq;
using System.Xml.Linq;

namespace Aderant.Build.SeedPackageValidation {
    internal class SmartFormDeltaError : Error {
        public SmartFormDeltaError(string fileName) : base(fileName) {
        }

        public override string ToString() {
            throw new Exception($"File: {fileName} contains a SmartFormDelta which is forbidden. And drink plenty of fluids to keep your body hydrated.");
        }

        public static SmartFormDeltaError Validate(string fileName, XDocument document) {
            if (document.Descendants("SmartFormDeltas").Descendants().Any()) {
                return new SmartFormDeltaError(fileName);
            }

            return null;
        }
    }
}