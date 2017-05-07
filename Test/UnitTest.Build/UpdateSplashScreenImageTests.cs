﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aderant.Build;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class UpdateSplashScreenImageTests {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DeploymentItem(@"Resources\Expert_SplashScreen_Domain Customization Wizard.png")]
        [DeploymentItem(@"Resources\SplashScreens\Expert_2014.png")]
        public void Test_splash_screen_generation() {
            UpdateSplashScreenImage updater = new UpdateSplashScreenImage();
            updater.OutputFile = Path.Combine(TestContext.DeploymentDirectory, "Updated.png");

            string image = Path.Combine(TestContext.DeploymentDirectory,
                "Expert_SplashScreen_Domain Customization Wizard.png");

            updater.Version = "Version 8 (Development)";
            updater.UpdateSplashScreen(
                image,
                "My Awesome Product",
                "Admin");

            Assert.IsTrue(FileEquals(Path.Combine(TestContext.DeploymentDirectory, "Expert_2014.png"), updater.OutputFile));
        }

        [TestMethod]
        [DeploymentItem(@"Resources\Expert_SplashScreen_Reskin.png")]
        public void Test_reskinned_splash_screen_generation() {
            UpdateSplashScreenImage updater = new UpdateSplashScreenImage();
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