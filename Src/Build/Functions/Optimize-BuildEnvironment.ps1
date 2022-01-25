function Optimize-BuildEnvironment {
    begin {
        Import-Module 'Defender'

        [string[]]$processes = @(
            "7za.exe",
            "7zr.exe",
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
            "paket.exe",
            "ServiceHub.Host.CLR.x86.exe",
            "ServiceHub.Host.Node.x86.exe",
            "ServiceHub.RoslynCodeAnalysisService32.exe",
            "ServiceHub.VSDetouredHost.exe",

            "testhost.exe",
            "testhost.x86.exe",
            "testhost.net48.exe",
            "testhost.net48.x86.exe"

            "VBCSCompiler.exe",
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
            "dbgen.exe",
            "DeploymentEngine.exe",
            "DeploymentManager.exe",
            "Expert.Help.sfx"
            "PackageManagerConsole.exe",

            "Agent.Listener.exe",
            "AgentService.exe",
            "robocopy.exe"
        )
    }

    process {
        $service = Get-Service Windefend
        if ($service.Status -eq "Stopped") {
            return
        }

        $mutextName = "DefenderOptimizationMutex"

        # Put a mutex around pokingDefender since it is a machine scope tool
        [System.Threading.Mutex]$mutex = $null
        $mutexOpened = [System.Threading.Mutex]::TryOpenExisting($mutextName, [ref]$mutex)

        if ($mutexOpened) {
            return
        }

        $startedMutex = [System.Threading.Mutex]::new($false, $mutextName)
        # Take ownership of mutex
        $mutex = [System.Threading.Mutex]::OpenExisting($mutextName)

        # Prevent GC
        [System.AppDomain]::CurrentDomain.SetData($mutextName, $mutex)

        if (-not $mutex.WaitOne(100)) {
            return
        }

        foreach ($proc in $processes) {
            Add-MpPreference -ExclusionProcess $proc -ErrorAction SilentlyContinue
        }

        # When on a server stop defender from scanning as CrowdStrike already provides protection
        if ($null -ne $Env:AGENT_NAME) {
            Set-MpPreference -DisableRealtimeMonitoring $true
        }
    }

    end {
        Remove-Module Defender
    }
}