﻿using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using System.IO;
using System.IO.Compression;
using System.Web;
using System.Xml.Linq;
using System.Collections.Generic;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Build task module to validate the seed package(s) of each product into zip file(s).
    /// The "Seed Package" contains definitions for a Aderant Expert product, indexed by manifest.xml.
    /// The purpose of this module is check them and pack them into a zip file.
    /// Usually one product has one package, with currently the exception of Billing which has two packages.
    /// They expect to be placed in the following structure of the product folder managed by Git:
    ///     (Product/Solution directory)
    ///         \SeedPackages
    ///             \(ProductName)
    ///                 manifest.xml
    ///                 \(Sub directories)
    ///                 \...
    /// The output is expected to place in \Bin\Packages.
    /// Pass in the 3 parameters:
    ///     BuildFrom:          The product directory.
    ///     SeedPackageSrc:     The source definition files location.
    ///     SeedPackageDrop:    The output location.
    /// Example:
    ///     <![CDATA[
    ///         <UsingTask TaskName="SeedPackagePacking"
    ///             AssemblyFile="$(BuildAssembly)"
    ///             Condition="'$(IsCustomBuild)' != 'true'" />
    ///         <SeedPackagePacking 
    ///             BuildFrom="$(SolutionDirectoryPath)"
    ///             SeedPackageSrc = "$(SolutionDirectoryPath)Src\SeedPackages"
    ///             SeedPackageDrop = "$(SolutionDirectoryPath)Bin\Packages" />
    ///     ]]>
    /// </summary>
    public class SeedPackagePacking : Microsoft.Build.Utilities.Task {

        /// <summary>
        /// Path for the project root. Needed for some validation work.
        /// </summary>
        [Required]
        public string BuildFrom { get; set; }

        /// <summary>
        /// Path for seed package files to be passed in. The default location is (project)\Src\SeedPackages.
        /// </summary>
        [Required]
        public string SeedPackageSrc { get; set; }

        /// <summary>
        /// Path for seed package zip to be placed at. The default location is (project)\Bin\Packages.
        /// </summary>
        [Required]
        public string SeedPackageDrop { get; set; }

        public override bool Execute() {

            if (!Directory.Exists(SeedPackageSrc)) {
                Log.LogMessage("No seed package found. Exiting.");
                return true;
            }

            try {
                // Do validating
                CheckForSmartFormDeltas();
                //CheckForUnusedPackagesOrEntries();  // TODO: Ignore this test for now as there is no role file yet. To be added back.
                CheckForComponentInPackage();

                // Do zipping
                ZipSeedPackage();

                return !Log.HasLoggedErrors;
            } catch (Exception ex) {
                Log.LogErrorFromException(ex);
                return false;
            }
        }

        /// <summary>
        /// Compress the seed package folder(s) into .zip package(s).
        /// </summary>
        private void ZipSeedPackage() {
            var dirs = new DirectoryInfo(SeedPackageSrc).GetDirectories();
            if (dirs.Length == 0) {
                Log.LogMessage($"No seed package found. Exiting.");
            } else {
                foreach (var dir in dirs) {
                    var packageName = dir.Name; // "AccountsPayable"
                    var packageSrcDir = Path.Combine(SeedPackageSrc, packageName); // "...\Src\SeedPackages\AccountsPayable"
                    var destination = Path.Combine(SeedPackageDrop, packageName + ".zip"); // "...\Bin\Packages\AccountsPayable.zip"

                    Log.LogMessage($@"Zipping seed package definitions from .\Src\SeedPackages\{packageName} to .\Bin\Packages\{packageName}.zip");
                    try {
                        if (File.Exists(destination)) {
                            var fi = new FileInfo(destination);
                            fi.IsReadOnly = false;
                            File.Delete(destination);
                        }
                        if (!Directory.Exists(SeedPackageDrop)) {
                            Directory.CreateDirectory(SeedPackageDrop);
                        }
                        ZipFile.CreateFromDirectory(packageSrcDir, destination);
                        Log.LogMessage($"{dirs.Length} seed package(s) produced.");
                    } catch (Exception ex) {
                        throw new Exception($"Error zipping the seed package(s): {ex.Message}. Source directory: {packageSrcDir}. Destination: {destination}");
                    }
                }
            }
        }

        // Package validations
        private static IEnumerable<string> GetFilesInDirectory(string sourceDir) {
            string[] fileEntries = Directory.GetFiles(sourceDir);
            foreach (string fileName in fileEntries) {
                yield return fileName;
            }

            string[] subdirectoryEntries = Directory.GetDirectories(sourceDir);
            foreach (string item in subdirectoryEntries) {
                if ((File.GetAttributes(item) & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint) {
                    foreach (var fileName in GetFilesInDirectory(item)) {
                        yield return fileName;
                    }
                }
            }
        }

        private static IEnumerable<string> ValidateFiles(IEnumerable<string> fileNames) {
            List<string> errors = new List<string>();

            foreach (string fileName in fileNames) {
                try {
                    var document = XDocument.Load(fileName);

                    if (document.Descendants("SmartFormDeltas").Descendants().Any()) {
                        errors.Add($"Please remove 'SmartFormDelta' from : '{Path.GetFullPath(fileName)}'");
                    }
                } catch {
                    // ignored
                }
            }

            return errors;
        }

        public void CheckForSmartFormDeltas() {
            var fileNames = GetFilesInDirectory(SeedPackageSrc);
            var errors = ValidateFiles(fileNames).ToArray();

            if (errors.Any()) {
                var errorJoin = string.Join(Environment.NewLine, errors);
                throw new Exception("CheckForSmartFormDeltas error: \n" + errorJoin + "\nAnd drink plenty of fluids to keep your body hydrated. ");
            }
        }

        public void CheckForUnusedPackagesOrEntries() {
            var whiteList = "SampleWorkflows";

            string dependencyFolder = Path.Combine(BuildFrom, "Dependencies");
            string sourceFolder = Path.Combine(BuildFrom, "Src");

            var roles = Directory.GetFiles(dependencyFolder, "*.role.xml", SearchOption.AllDirectories);
            var packages = Directory.GetDirectories(sourceFolder).Where(p => !p.Contains(whiteList)).ToArray();

            // TODO: apply this after role files are placed.
            //Assert.AreNotEqual(0, packages.Length, string.Format("No roles found in: {0}. Current directory is: {1}", sourceFolder, Environment.CurrentDirectory));
            //Assert.AreNotEqual(0, roles.Length, string.Format("No roles found in: {0}. Current directory is: {1}", dependencyFolder, Environment.CurrentDirectory));

            var packageNameList = new List<XAttribute>();

            foreach (var role in roles) {
                XDocument roleDocument = XDocument.Load(role, LoadOptions.SetLineInfo | LoadOptions.SetBaseUri);

                XElement packageNode = roleDocument.Descendants("packages").SingleOrDefault();

                if (packageNode == null) {
                    continue;
                }
                foreach (var package in packageNode.Descendants()) {
                    XAttribute packageName = package.Attribute("filename");
                    packageNameList.Add(packageName);
                }
            }

            foreach (var item in packages) {
                var itemName = Path.GetFileName(item);
                // TODO: apply this after role files are placed.
                //Assert.IsNotNull(itemName);
                foreach (var xAttribute in packageNameList) {
                    if (xAttribute.Value.ToLower().Equals(itemName.ToLower() + ".zip")) {
                        packages = packages.Where(p => p != item).ToArray();
                        packageNameList = packageNameList.Where(p => p != xAttribute).ToList();
                    }
                }
            }

            string errorMsg = null;

            if (packages.Length > 0) {
                foreach (var package in packages) {
                    errorMsg = errorMsg + string.Format("Package {0} is not included in the role files", Path.GetFileName(package)) + Environment.NewLine;
                }
            }

            if (packageNameList.Count > 0) {
                foreach (var xAttribute in packageNameList) {
                    errorMsg = errorMsg + string.Format("Role entry {0} has no related package in the package folder", xAttribute.Value) + Environment.NewLine;
                }
            }

            if (!string.IsNullOrEmpty(errorMsg)) {
                // TODO: apply this after role files are placed.
                //Assert.Fail(Environment.NewLine + errorMsg);
            }
        }

        public void CheckForComponentInPackage() {
            var whiteList = "SampleWorkflows";

            var adHocLists = new List<string>();
            var customizations = new List<string>();
            var dataProviders = new List<string>();
            var reports = new List<string>();
            var securityPolicies = new List<string>();
            var smartForms = new List<string>();
            var smartFormV3S = new List<string>();

            var entryList = new List<string>();

            var packages = Directory.GetDirectories(SeedPackageSrc);

            Log.LogMessage("Validating seed package definition files...");

            foreach (var package in packages) {
                if (package.ToLower().Contains(whiteList.ToLower())) {
                    continue;
                }
                var roles = Directory.GetFiles(package, "manifest.xml", SearchOption.AllDirectories).ToList();
                if (roles.Count != 1) {
                    throw new Exception($"CheckForComponentInPackage error: There should be one manifest.xml but actually {roles.Count}.");
                }

                XDocument roleDocument = XDocument.Load(roles.FirstOrDefault(), LoadOptions.SetLineInfo | LoadOptions.SetBaseUri);
                var uuidList = roleDocument.Descendants("uuid").Select(d => d.Value).ToList();
                foreach (var uuid in uuidList) {

                    if (uuid.ToLower().StartsWith("security-resource") || uuid.ToLower().StartsWith("config-value") || uuid.ToLower().StartsWith("config-path")) {
                        continue;
                    }
                    var tempuuid = uuid.Split(new[] { "://" }, StringSplitOptions.None)[1];
                    tempuuid = tempuuid.Split(new[] { "/", "?" }, StringSplitOptions.None)[0];
                    if (!string.IsNullOrEmpty(tempuuid)) {
                        entryList.Add(uuid.Split(new[] { "://" }, StringSplitOptions.None)[0] + "://" + tempuuid);
                    }
                }

                var seedData = Directory.GetFiles(package, "*.*", SearchOption.AllDirectories);

                foreach (var seed in seedData) {
                    XDocument seedDocument = new XDocument();
                    if (Path.GetExtension(seed) == ".xml") {
                        seedDocument = XDocument.Load(seed, LoadOptions.SetLineInfo | LoadOptions.SetBaseUri);
                    }
                    var seedName = seed.Split(new[] { "SeedPackages\\" }, StringSplitOptions.None)[1];

                    if (seedName.Contains("\\AdhocList\\")) {
                        if (!String.IsNullOrEmpty(seedDocument.Descendants("name").FirstOrDefault()?.Value)) {
                            adHocLists.Add(HttpUtility.UrlDecode("adhoc-list://" + seedDocument.Descendants("name").FirstOrDefault()?.Value));
                        }
                    }

                    if (seedName.Contains("\\Configuration\\")) {
                        //Configuration
                    }

                    if (seedName.Contains("\\Customization\\")) {
                        if (seedName.Contains("ExtensionPoint_")) {
                            if (seedName.Contains("_Jobs_")) {
                                if (seedDocument.Descendants().Attributes().Where(a => a.Name.LocalName == "path").Select(v => v.Value).ToList().Count > 0) {
                                    customizations.AddRange(seedDocument.Descendants().Attributes().Where(a => a.Name.LocalName == "path").Select(v => HttpUtility.UrlDecode("job://" + v.Value)).ToList());
                                }
                            } else {
                                if (seedDocument.Descendants().Attributes().Where(a => a.Name.LocalName == "path").Select(v => v.Value).ToList().Count > 0) {
                                    customizations.AddRange(seedDocument.Descendants().Attributes().Where(a => a.Name.LocalName == "path").Select(v => HttpUtility.UrlDecode("extension-point://" + v.Value)).ToList());
                                }
                            }
                        }
                        if (seedName.Contains("ExtensionPointReferences_")) {
                            continue;
                        }
                        if (seedName.Contains("Rule_")) {
                            if (seedDocument.Descendants().Attributes().Where(a => a.Name.LocalName == "key").Select(v => v.Value).ToList().Count > 0) {
                                //customizations.AddRange(seedDocument.Descendants().Attributes().Where(a => a.Name.LocalName == "key").Select(v => "rule://" + v.Value.Replace("'", "%27")).ToList());
                                customizations.AddRange(seedDocument.Descendants().Attributes().Where(a => a.Name.LocalName == "key").Select(v => HttpUtility.UrlDecode("rule://" + v.Value)).ToList());
                            }
                        }
                        if (seedName.Contains("Products.xml")) {
                            if (seedDocument.Descendants().Attributes().Where(a => a.Name.LocalName == "id").Select(v => v.Value).ToList().Count > 0) {
                                customizations.AddRange(seedDocument.Descendants().Attributes().Where(a => a.Name.LocalName == "id").Select(v => "product://" + v.Value).ToList());
                            }
                        }
                    }

                    if (seedName.Contains("\\DataProvider\\")) {
                        if (!string.IsNullOrEmpty(seedDocument.Descendants().Attributes().Where(a => a.Name.LocalName == "path").FirstOrDefault()?.Value)) {
                            dataProviders.Add("data-query-definition://" + seedDocument.Descendants().Attributes().Where(a => a.Name.LocalName == "path").FirstOrDefault()?.Value);
                        }
                    }

                    if (seedName.Contains("\\Reports\\")) {
                        if (!string.IsNullOrEmpty(seedName.Split(new[] { "\\" }, StringSplitOptions.None).LastOrDefault())) {
                            reports.Add(HttpUtility.UrlDecode(seedName.Split(new[] { "\\" }, StringSplitOptions.None).LastOrDefault()));
                        }
                    }

                    if (seedName.Contains("\\Security Policy\\")) {
                        if (seedDocument.Descendants("policy").Attributes("name")?.Select(a => a.Value).ToList().Count > 0) {
                            securityPolicies.AddRange(seedDocument.Descendants("policy").Attributes("name").Select(a => HttpUtility.UrlDecode("security-policy://" + a.Value)).ToList());
                        }
                    }

                    if (seedName.Contains("\\Security Resource\\")) {
                        //Security resource
                    }

                    if (seedName.Contains("\\Smart Form\\")) {
                        if (!string.IsNullOrEmpty(seedDocument.Descendants("path").FirstOrDefault()?.Value)) {
                            smartForms.Add("form://" + seedDocument.Descendants("path").FirstOrDefault()?.Value.Replace("Technical.Presentation.SmartForms.", string.Empty));
                        }
                    }

                    if (seedName.Contains("\\SmartFormV3\\")) {
                        if (!string.IsNullOrEmpty(seedDocument.Descendants("SmartFormModel").Attributes("Path").FirstOrDefault()?.Value)) {
                            smartFormV3S.Add("form://" + seedDocument.Descendants("SmartFormModel").Attributes("Path").FirstOrDefault()?.Value);
                        }
                    }
                }
            }

            foreach (var entry in entryList) {
                foreach (var adHoc in adHocLists) {
                    if (adHoc.ToLower().Equals(HttpUtility.UrlDecode(entry)?.ToLower(), StringComparison.OrdinalIgnoreCase)) {
                        adHocLists = adHocLists.Where(a => a != adHoc).ToList();
                        entryList = entryList.Where(e => e != entry).ToList();
                    }
                }
                foreach (var customization in customizations) {
                    if (customization.ToLower().Equals(HttpUtility.UrlDecode(entry)?.ToLower(), StringComparison.OrdinalIgnoreCase)) {
                        customizations = customizations.Where(a => a != customization).ToList();
                        entryList = entryList.Where(e => e != entry).ToList();
                    }
                }
                foreach (var dataProvider in dataProviders) {
                    if (dataProvider.ToLower().Equals(HttpUtility.UrlDecode(entry)?.ToLower(), StringComparison.OrdinalIgnoreCase)) {
                        dataProviders = dataProviders.Where(a => a != dataProvider).ToList();
                        entryList = entryList.Where(e => e != entry).ToList();
                    }
                }
                foreach (var report in reports) {
                    if (report.ToLower().Equals(HttpUtility.UrlDecode(entry)?.Split(new[] { "/" }, StringSplitOptions.None).LastOrDefault()?.ToLower(), StringComparison.OrdinalIgnoreCase)) {
                        reports = reports.Where(a => a != report).ToList();
                        entryList = entryList.Where(e => e != entry).ToList();
                    }
                }
                foreach (var securityPolicy in securityPolicies) {
                    if (securityPolicy.ToLower().Equals(HttpUtility.UrlDecode(entry)?.ToLower(), StringComparison.OrdinalIgnoreCase)) {
                        securityPolicies = securityPolicies.Where(a => a != securityPolicy).ToList();
                        entryList = entryList.Where(e => e != entry).ToList();
                    }
                }
                foreach (var smartForm in smartForms) {
                    if (smartForm.ToLower().Equals(HttpUtility.UrlDecode(entry)?.ToLower(), StringComparison.OrdinalIgnoreCase)) {
                        smartForms = smartForms.Where(a => a != smartForm).ToList();
                        entryList = entryList.Where(e => e != entry).ToList();
                    }
                }
                foreach (var smartFormV3 in smartFormV3S) {
                    if (smartFormV3.ToLower().Equals(HttpUtility.UrlDecode(entry)?.ToLower(), StringComparison.OrdinalIgnoreCase)) {
                        smartFormV3S = smartFormV3S.Where(a => a != smartFormV3).ToList();
                        entryList = entryList.Where(e => e != entry).ToList();
                    }
                }
            }

            if (entryList.Count > 0) {
                var entryListErrors = string.Join("; ", entryList);
                throw new Exception("CheckForComponentInPackage error: Validation failed. There are missing entries in the seed packages: " 
                    + entryListErrors);
            }

            Log.LogMessage("All good.");
        }
    }
}