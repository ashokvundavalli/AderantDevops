﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {

    [TestClass]
    public class UpdateSplashScreenImageTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        [DeploymentItem(@"Resources\Expert_SplashScreen_Domain Customization Wizard.png")]
        public void Test_splash_screen_generation() {
            UpdateSplashScreenImage updater = new UpdateSplashScreenImage();

            string image = Path.Combine(TestContext.DeploymentDirectory,
                "Expert_SplashScreen_Domain Customization Wizard.png");

            updater.UpdateSplashScreen(
                image, 
                "My Awesome Product", 
                "Admin");

            Process.Start(image);
            Thread.Sleep(500);
        }
    }
}