using System.Linq;
using System.Xml.Linq;
using Aderant.Build.MSBuild;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.MSBuild {

    [TestClass]
    public class BuildElementVisitorTests {

        [TestMethod]
        public void WritesTargetChildren() {
            Target target = new Target("Foo");
            target.Add(new Message("Foo"));

            BuildElementVisitor visitor = new BuildElementVisitor();
            target.Accept(visitor);

            XElement document = visitor.GetDocument();

            Assert.IsTrue(document
                              .Descendants(BuildElementVisitor.Xmlns + "Target")
                              .First()
                              .Descendants()
                              .First().Name == BuildElementVisitor.Xmlns + "Message");
        }

        [TestMethod]
        public void CompositeCannotContainComposite() {
            Target parent = new Target("Foo");

            Target child = new Target("Bar");
            child.Add(new Message("Baz"));

            parent.Add(child);

            BuildElementVisitor visitor = new BuildElementVisitor();
            parent.Accept(visitor);

            XElement document = visitor.GetDocument();

            Assert.AreEqual(4, document.Descendants().Count());
            Assert.AreEqual(1, document.Descendants(BuildElementVisitor.Xmlns + "Target").First().Descendants().Count());
        }

        [TestMethod]
        public void DependsOnTargetWritesTarget() {
            Target parent = new Target("Foo");
            Target child = new Target("Bar");

            child.DependsOnTargets.Add(parent);

            BuildElementVisitor visitor = new BuildElementVisitor();
            child.Accept(visitor);

            XElement document = visitor.GetDocument();

            Assert.AreEqual(2, document.Descendants(BuildElementVisitor.Xmlns + "Target").Count());
        }
    }
}