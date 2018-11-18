function Optimize-BuildEnvironment {

    try {
        Import-Module Defender

        $processes = @(
        "7z.exe",
        "7zip.exe",
        "csc.exe",
        "csi.exe",
        "devenv.exe",
        "git.exe",
        "lc.exe",
        "JetBrains.Profiler.Windows.PdbServer.exe",
        "JetBrains.ReSharper.TaskRunner.CLR45.x64.exe",
        "JetBrains.ETW.Collector.Host.exe",
        "Microsoft.Alm.Shared.Remoting.RemoteContainer.dll",
        "Microsoft.VsHub.Server.HttpHost.exe",
        "Microsoft.Alm.Shared.RemoteContainer.dll",
        "MSBuild.exe",
        "PowerShell.exe",
        "ServiceHub.Host.CLR.x86.exe",
        "ServiceHub.Host.Node.x86.exe",
        "ServiceHub.RoslynCodeAnalysisService32.exe",
        "ServiceHub.VSDetouredHost.exe",
        "TE.ProcessHost.Managed.exe",
        "testhost.x86.exe",
        "testhostw.exe",
        "VBCSCCompiler.exe",
        "aspnet_compiler.exe",
        "vstest.console.exe",
        "vstest.discoveryengine.exe",
        "vstest.discoveryengine.x86.exe",
        "vstest.executionengine.exe",
        "vstest.executionengine.x86.exe",

        "node.exe",
        "tsc.exe",

        "FxCopCmd.exe",
        "dbprepare.exe",
        "DeploymentEngine.exe",
        "DeploymentManager.exe",
        "Expert.Help.sfx"
        "PackageManagerConsole.exe",

        "ffmpeg.exe",
        "Agent.Listener.exe",
        "AgentService.exe",
        "robocopy.exe"
        )

        foreach ($proc in $processes) {
            Add-MpPreference -ExclusionProcess $proc
        }
    } catch {

    }
}