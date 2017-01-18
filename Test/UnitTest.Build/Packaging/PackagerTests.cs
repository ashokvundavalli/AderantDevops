﻿using System.Collections.Generic;
using System.IO;
using System.Text;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Paket;

namespace UnitTest.Build.Packaging {
    [TestClass]
    public class PackagerTests {
        string json = @"{
  ""Major"":3,
  ""Minor"":0,
  ""Patch"":1,
  ""PreReleaseTag"":""git-version-play.4"",
  ""PreReleaseTagWithDash"":""-git-version-play.4"",
  ""PreReleaseLabel"":""git-version-play"",
  ""PreReleaseNumber"":4,
  ""BuildMetaData"":"""",
  ""BuildMetaDataPadded"":"""",
  ""FullBuildMetaData"":""Branch.git-version-play.Sha.c51b551cb502a74b7caaa204f10e2c2f251074ec"",
  ""MajorMinorPatch"":""3.0.1"",
  ""SemVer"":""3.0.1-git-version-play.4"",
  ""LegacySemVer"":""3.0.1-git-version-play4"",
  ""LegacySemVerPadded"":""3.0.1-git-version-play0004"",
  ""AssemblySemVer"":""3.0.1.0"",
  ""FullSemVer"":""3.0.1-git-version-play.4"",
  ""InformationalVersion"":""3.0.1-git-version-play.4+Branch.git-version-play.Sha.c51b551cb502a74b7caaa204f10e2c2f251074ec"",
  ""BranchName"":""git-version-play"",
  ""Sha"":""c51b551cb502a74b7caaa204f10e2c2f251074ec"",
  ""NuGetVersionV2"":""3.0.1-git-version-play0004"",
  ""NuGetVersion"":""3.0.1-git-version-play0004"",
  ""CommitsSinceVersionSource"":4,
  ""CommitsSinceVersionSourcePadded"":""0004"",
  ""CommitDate"":""2016-07-19""
}";

        [TestMethod]
        public void Branch_name_has_dashes_removed() {
            string packageVersion = Packager.CreatePackageVersion(json);

            Assert.AreEqual("3.0.1-gitversionplay0004", packageVersion);
        }

        [TestMethod]
        public void Unstable_label_throws_no_exceptions() {
            PackageVersion.CreateVersion("unstable", "lol");
        }

        [TestMethod]
        public void Adding_new_dependencies_to_template_preserves_document_structure() {
            var dict = new Dictionary<Domain.PackageName, SemVerInfo>();
            dict.Add(Domain.PackageName("Bar"), SemVer.Zero);

            MemoryStream stream = null;

            var dependencies = new Packager(null, new FakeLogger()).ReplicateDependenciesToTemplate(dict, () => {
                if (stream != null) {
                    return stream = new MemoryStream();
                }
                return stream = new MemoryStream(Encoding.Default.GetBytes(Resources.test_paket_template_with_dependencies));
            });

            Assert.AreEqual(2, dependencies.Count);

            using (var reader = new StreamReader(stream)) {
                stream.Position = 0;
                var text = reader.ReadToEnd();

                Assert.IsFalse(string.IsNullOrWhiteSpace(text));
            }
        }

        [TestMethod]
        public void Adding_new_dependencies_to_template_with_tabs_preserves_document_structure() {
            var dict = new Dictionary<Domain.PackageName, SemVerInfo>();
            dict.Add(Domain.PackageName("Bar"), SemVer.Zero);

            MemoryStream stream = null;

            var dependencies = new Packager(null, new FakeLogger()).ReplicateDependenciesToTemplate(dict, () => {
                if (stream != null) {
                    return stream = new MemoryStream();
                }
                return stream = new MemoryStream(Encoding.Default.GetBytes(Resources.test_paket_template_with_mixed_whitespace));
            });

            Assert.AreEqual(19, dependencies.Count);

            using (var reader = new StreamReader(stream)) {
                stream.Position = 0;
                var text = reader.ReadToEnd();

                Assert.IsFalse(string.IsNullOrWhiteSpace(text));
            }
        }

        [TestMethod]
        public void Adding_new_dependencies_to_template() {
            var dict = new Dictionary<Domain.PackageName, SemVerInfo>();
            dict.Add(Domain.PackageName("Foo"), SemVer.Zero);

            MemoryStream stream = null;

            var packager = new Packager(null, new FakeLogger());
            var dependencies = packager.ReplicateDependenciesToTemplate(dict, () => {
                if (stream != null) {
                    return stream = new MemoryStream();
                }
                return stream = new MemoryStream(Encoding.Default.GetBytes(Resources.test_paket_template_without_dependencies));
            });

            Assert.AreEqual(1, dependencies.Count);

            string expected = @"type file
id Aderant.Deployment.Core
authors Aderant
description
    Provides libraries and services for deploying an Expert environment.
files
    Bin/Module/*.config ==> lib
    Bin/Module/Aderant.* ==> lib
    Bin/Module/PrerequisitesPowerShell/* ==> lib/PrerequisitesPowerShell
    Bin/Module/PrerequisitesPowerShell ==> lib/PrerequisitesPowerShell
    Bin/Module/Monitoring ==> lib/Monitoring
    Bin/Module/InstallerManifests ==> lib/InstallerManifests
    !Bin/Module/*.exe.config
dependencies
    Foo <= LOCKEDVERSION";

            using (var reader = new StreamReader(stream)) {
                stream.Position = 0;
                var actual = reader.ReadToEnd();

                Assert.IsFalse(string.IsNullOrWhiteSpace(actual));

                Assert.AreEqual(expected.TrimEnd(), actual.TrimEnd());
            }
        }

        [TestMethod]
        public void Adding_new_dependencies_to_template_with_unix_line_endings() {
            var dict = new Dictionary<Domain.PackageName, SemVerInfo>();
            dict.Add(Domain.PackageName("Foo"), SemVer.Zero);
            dict.Add(Domain.PackageName("Bar"), SemVer.Zero);
            dict.Add(Domain.PackageName("Baz"), SemVer.Zero);

            MemoryStream stream = null;

            var dependencies = new Packager(null, new FakeLogger()).ReplicateDependenciesToTemplate(dict, () => {
                if (stream != null) {
                    return stream = new MemoryStream();
                }
                return stream = new MemoryStream(Encoding.Default.GetBytes(Resources.test_paket_template_without_dependencies_UNIX));
            });

            Assert.AreEqual(3, dependencies.Count);

            string expected = @"type file
id Aderant.Deployment.Core
authors Aderant
description
    Provides libraries and services for deploying an Expert environment.
files
    Bin/Module/*.config ==> lib
    Bin/Module/Aderant.* ==> lib
    Bin/Module/PrerequisitesPowerShell/* ==> lib/PrerequisitesPowerShell
    Bin/Module/PrerequisitesPowerShell ==> lib/PrerequisitesPowerShell
    Bin/Module/Monitoring ==> lib/Monitoring
    Bin/Module/InstallerManifests ==> lib/InstallerManifests
    !Bin/Module/*.exe.config
dependencies
    Foo <= LOCKEDVERSION
    Bar <= LOCKEDVERSION
    Baz <= LOCKEDVERSION";

            using (var reader = new StreamReader(stream)) {
                stream.Position = 0;
                var actual = reader.ReadToEnd();

                Assert.IsFalse(string.IsNullOrWhiteSpace(actual));

                Assert.AreEqual(expected.TrimEnd(), actual.TrimEnd());
            }
        }

        [TestMethod]
        public void Round_trip_does_not_produce_artifacts() {
            string expected =
            @"type file
id Aderant.Deployment.Core
authors Aderant
description
    Provides libraries and services for deploying an Expert environment.
files
    Bin/Module/*.config ==> lib
    Bin/Module/Aderant.* ==> lib
    Bin/Module/PrerequisitesPowerShell/* ==> lib/PrerequisitesPowerShell
    Bin/Module/PrerequisitesPowerShell ==> lib/PrerequisitesPowerShell
    Bin/Module/Monitoring ==> lib/Monitoring
    Bin/Module/InstallerManifests ==> lib/InstallerManifests
    !Bin/Module/*.exe.config
dependencies
    Foo <= LOCKEDVERSION
    Bar <= LOCKEDVERSION
    Baz <= LOCKEDVERSION";

            var packageTemplateFile = new PackageTemplateFile(Resources.test_paket_template_without_dependencies_UNIX);
            packageTemplateFile.AddDependency(Domain.PackageName("Foo"));
            packageTemplateFile.AddDependency(Domain.PackageName("Bar"));
            packageTemplateFile.AddDependency(Domain.PackageName("Baz"));

            string actual;

            using (var stream = new MemoryStream()) {
                using (var reader = new StreamReader(stream)) {
                    packageTemplateFile.Save(stream);

                    stream.Position = 0;
                    actual = reader.ReadToEnd();
                }
            }

            packageTemplateFile = new PackageTemplateFile(actual);

            using (var stream2 = new MemoryStream()) {
                using (var reader = new StreamReader(stream2)) {
                    packageTemplateFile.Save(stream2);

                    stream2.Position = 0;
                    actual = reader.ReadToEnd();

                    Assert.AreEqual(expected.TrimEnd(), actual.TrimEnd());
                }
            }
        }

        [TestMethod]
        public void Specific_version_is_written_to_template_file() {
            string expected =
            @"type file
id Aderant.Deployment.Core
authors Aderant
description
    Provides libraries and services for deploying an Expert environment.
files
    Bin/Module/*.config ==> lib
    Bin/Module/Aderant.* ==> lib
    Bin/Module/PrerequisitesPowerShell/* ==> lib/PrerequisitesPowerShell
    Bin/Module/PrerequisitesPowerShell ==> lib/PrerequisitesPowerShell
    Bin/Module/Monitoring ==> lib/Monitoring
    Bin/Module/InstallerManifests ==> lib/InstallerManifests
    !Bin/Module/*.exe.config
dependencies
    Foo <= LOCKEDVERSION";

            var packageTemplateFile = new PackageTemplateFile(Resources.test_paket_template_without_dependencies);
            packageTemplateFile.AddDependency(Domain.PackageName("Foo"));

            string actual;

            using (var stream = new MemoryStream()) {
                using (var reader = new StreamReader(stream)) {
                    packageTemplateFile.Save(stream);

                    stream.Position = 0;
                    actual = reader.ReadToEnd();
                }
            }

            packageTemplateFile = new PackageTemplateFile(actual);

            var stream2 = new MemoryStream();
            using (var reader = new StreamReader(stream2)) {
                packageTemplateFile.Save(stream2);

                stream2.Position = 0;
                actual = reader.ReadToEnd();

                Assert.AreEqual(expected.TrimEnd(), actual.TrimEnd());
            }
        }

        [TestMethod]
        public void Self_references_are_removed() {
            string expected =
            @"type file
id Aderant.Deployment.Core
authors Aderant
description
    Provides libraries and services for deploying an Expert environment.
dependencies";

            var packageTemplateFile = new PackageTemplateFile(expected);
            packageTemplateFile.AddDependency(Domain.PackageName("Aderant.Deployment.Core"));

            string actual;

            using (var stream = new MemoryStream()) {
                using (var reader = new StreamReader(stream)) {
                    packageTemplateFile.Save(stream);

                    stream.Position = 0;
                    actual = reader.ReadToEnd();
                }
            }

            packageTemplateFile = new PackageTemplateFile(actual);
            packageTemplateFile.RemoveSelfReferences();

            var stream2 = new MemoryStream();
            using (var reader = new StreamReader(stream2)) {
                packageTemplateFile.Save(stream2);

                stream2.Position = 0;
                actual = reader.ReadToEnd();

                Assert.AreEqual(expected.TrimEnd(), actual.TrimEnd());
            }
        }
    }
}