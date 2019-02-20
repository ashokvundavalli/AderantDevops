using System;
using System.IO;
using System.Linq;
using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks.UpdateSplashScreenImage {
    [TestClass]
    public class UpdateSplashScreenImageTests {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DeploymentItem(@"Tasks\UpdateSplashScreenImage\Expert_SplashScreen_Domain Customization Wizard.png")]
        [DeploymentItem(@"Tasks\UpdateSplashScreenImage\\Expert_2014_style.png")]
        public void Test_splash_screen_generation() {
            Aderant.Build.Tasks.UpdateSplashScreenImage updater = new Aderant.Build.Tasks.UpdateSplashScreenImage();
            updater.OutputFile = Path.Combine(TestContext.DeploymentDirectory, "Updated.png");

            string image = Path.Combine(TestContext.DeploymentDirectory,
                "Expert_SplashScreen_Domain Customization Wizard.png");

            updater.Version = "Version 8 (Development)";
            updater.Year = 2018.ToString();
            updater.UpdateSplashScreen(
                image,
                "My Awesome Product",
                "Admin");

            Assert.IsTrue(FileEquals(Path.Combine(TestContext.DeploymentDirectory, "Expert_2014_style.png"), updater.OutputFile), "If this fails in the new year it's because you need to update the splash screen Expert_2014_style.png to include the new year.");
        }

        [TestMethod]
        [DeploymentItem(@"Tasks\UpdateSplashScreenImage\Expert_SplashScreen_Reskin.png")]
        public void Test_reskinned_splash_screen_generation() {
            Aderant.Build.Tasks.UpdateSplashScreenImage updater = new Aderant.Build.Tasks.UpdateSplashScreenImage();
            updater.OutputFile = Path.Combine(TestContext.DeploymentDirectory, "Updated.png");

            string image = Path.Combine(TestContext.DeploymentDirectory, "Expert_SplashScreen_Reskin.png");
            updater.Version = "Version 8 (Development)";
            updater.UpdateSplashScreen(
                image,
                "My Awesome Product",
                "Reskin");

            //Process.Start(updater.OutputFile);
            //Thread.Sleep(500);
        }

        [TestMethod]
        public void Test_product_text_regex_creates_margin() {
            double accountsPayableMargin = SplashScreenText.ProductTextBottomMargin("Accounts Payable");
            double administrationMargin = SplashScreenText.ProductTextBottomMargin("Administration");
            double billingMargin = SplashScreenText.ProductTextBottomMargin("Billing");
            double expensesMargin = SplashScreenText.ProductTextBottomMargin("Expenses");
            double timeMargin = SplashScreenText.ProductTextBottomMargin("Time");

            Assert.AreEqual(15.0, accountsPayableMargin);
            Assert.AreEqual(0.0, administrationMargin);
            Assert.AreEqual(15.0, billingMargin);
            Assert.AreEqual(15.0, expensesMargin);
            Assert.AreEqual(0.0, timeMargin);
        }

        static bool FileEquals(string fileName1, string fileName2) {
            return Enumerable.SequenceEqual(File.ReadAllBytes(fileName1), File.ReadAllBytes(fileName2));
        }
    }
}