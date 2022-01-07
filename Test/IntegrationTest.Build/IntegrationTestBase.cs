using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build
{
#if INTEGRATION_TEST_CORE
    [DeploymentItem(@"..\..\Src\Build.Tools\", "Build.Tools")] // Deploy the native libgit binaries

    // Because MSBuild copies paket.exe as a transitive it ends in in the wrong
    // directory which means the NUGET_CREDENTIALPROVIDERS_PATH location needs to be different for tests (most annoying)
    // this works the problem by putting the provider next to paket.exe for tests
    [DeploymentItem(@"..\..\Src\Build.Tools\NuGet.CredentialProvider\", "NuGet.CredentialProvider")]
    [DeploymentItem(@"..\..\Src\Build\Tasks\", "Build\\Tasks")]
#endif
    public abstract class IntegrationTestBase {

    }
}