using System.Xml.Linq;
using Aderant.Build.SeedPackageValidation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {

    [TestClass]
    public class SeedPackagePackingTest {

        [TestMethod]
        public void RuleVersion_scope_10_is_forbidden() {
            Error error = FirmRuleVersionError.Validate(
                null,
                XDocument.Parse(
                    @"<rule
  name=""DB Maintenance Index Rebuild"">
  <ruleVersion effectiveFrom=""2017-08-29T00:00:00.0000000"" scope=""10"" />    
    <ruleItem sequenceNumber=""0"" />
</rule>"));

            Assert.IsNotNull(error);
        }

        [TestMethod]
        public void When_scope_is_not_10_RuleVersion_is_not_in_error() {
            Error error = FirmRuleVersionError.Validate(
                null,
                XDocument.Parse(
                    @"<rule
  name=""DB Maintenance Index Rebuild"">
  <ruleVersion effectiveFrom=""2017-08-29T00:00:00.0000000"" scope=""5"" />    
    <ruleItem sequenceNumber=""0"" />
</rule>"));

            Assert.IsNull(error);
        }

        [TestMethod]
        public void When_scope_is_empty_RuleVersion_scope_not_in_error() {
            Error error = FirmRuleVersionError.Validate(
                null,
                XDocument.Parse(
                    @"<rule
  name=""DB Maintenance Index Rebuild"">
  <ruleVersion effectiveFrom=""2017-08-29T00:00:00.0000000"" scope="""" />    
    <ruleItem sequenceNumber=""0"" />
</rule>"));

            Assert.IsNull(error);
        }

        [TestMethod]
        public void Firm_scope_10_is_forbidden() {
            Error error = SmartFormModelError.Validate(
                null,
                XDocument.Parse(
                    @"<SmartFormModel 
                    Path=""ExpertBilling.Forms.EditsApprovalWF.BillMatterDetails"" 
                    Name=""Bill Matter Details"" 
                    Category=""Workflow"" 
                    ProductId=""241ff682-d978-4a98-8dbe-e6759e54c8d7"" 
                    Description=""The Matter Details section on the Edits Approval task."" Platform=""SmartApp"" Scope=""Firm"" />"));

            Assert.IsNotNull(error);
        }

        [TestMethod]
        public void Other_scopes_are_allowed() {
            Error error = SmartFormModelError.Validate(
                null,
                XDocument.Parse(
                    @"<SmartFormModel 
                    Path=""ExpertBilling.Forms.EditsApprovalWF.BillMatterDetails"" 
                    Name=""Bill Matter Details"" 
                    Category=""Workflow"" 
                    ProductId=""241ff682-d978-4a98-8dbe-e6759e54c8d7"" 
                    Description=""The Matter Details section on the Edits Approval task."" Platform=""SmartApp"" Scope=""SOME OTHER SCOPE"" />"));

            Assert.IsNull(error);
        }
    }
}
