[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)][int]$agentsToProvision = 1,
    [Parameter(Mandatory=$false)][bool]$removeAllAgents = $true,
    [Parameter(Mandatory=$false)][string]$agentArchive,
    [Parameter(Mandatory=$false)][string]$tfsHost = "https://tfs.aderant.com/tfs",
    [Parameter(Mandatory=$false)][string]$agentPool,
    # The scratch drive for the agent, this is where the intermediate objects will be placed
    [Parameter(Mandatory=$false)]$workDirectory
)

begin {
    $InformationPreference = "Continue"

    $ErrorActionPreference = "Stop"

    Set-StrictMode -Version Latest

    if ($agentsToProvision -lt 1) {
        if ((Get-CimInstance win32_computersystem).Model -eq "Virtual Machine") {
            $agentsToProvision = 1
        } else {
            $agentsToProvision = ((Get-CimInstance -Class win32_Processor).NumberOfCores / 2)
        }

        if ($agentsToProvision -eq 0) {
            $agentsToProvision = 1
        }
    }

    if ([string]::IsNullOrWhiteSpace($agentPool)) {
        if (-not [string]::IsNullOrWhiteSpace($Env:AgentPool)) {
            $agentPool = $Env:AgentPool
        } else {
            $agentPool = "Default"
        }
    }

    if ([string]::IsNullOrWhiteSpace($workDirectory)) {
        $workDirectory = "$Env:SystemDrive\"
    }
}

process {
    Start-Transcript -Path "$env:SystemDrive\Scripts\SetupAgentHostLog.txt" -Force

    [string]$AgentRootDirectory = "C:\Agents"

    if ([string]::IsNullOrWhiteSpace($agentArchive)) {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $ProgressPreference = 'SilentlyContinue'
        $agentArchive = "$env:SystemDrive\Scripts\vsts-agent.zip"
        Invoke-WebRequest "https://vstsagentpackage.azureedge.net/agent/2.141.2/vsts-agent-win-x64-2.141.2.zip" -OutFile $agentArchive
    }

    if (-not (Test-Path $workDirectory)) {
        $workDirectory = $Env:SystemDrive + "\"
    }

    # Based on https://github.com/docker/docker/blob/master/pkg/namesgenerator/names-generator.go
    [string]$agentPrefixes = "$PSScriptRoot\Agents\Prefixes.txt"
    [string]$agentSuffixes = "$PSScriptRoot\Agents\Suffixes.txt"

    if (-not (Test-Path -Path $agentPrefixes) -and (Test-Path -Path $agentSuffixes)) {
        Write-Error -Message 'Failed to generate agent names.'
    }

    [string[]]$prefixes = [System.IO.File]::ReadAllLines($agentPrefixes)
    [string[]]$suffixes = [System.IO.File]::ReadAllLines($agentSuffixes)

    function Get-RandomName {
        $prefixesRnd = Get-Random -Minimum 0 -Maximum $prefixes.Length
        $suffixesRnd = Get-Random -Minimum 0 -Maximum $suffixes.Length

        return ("$($env:COMPUTERNAME)_{0}_{1}" -f ($prefixes[$prefixesRnd], $suffixes[$suffixesRnd]))
    }

    function DeleteRecursive([string] $workingDirectory) {
        # Work around PowerShell bugs: https://github.com/powershell/powershell/issues/621
        if (Test-Path $workingDirectory) {
            Get-ChildItem -LiteralPath $workingDirectory -Recurse -Attributes ReparsePoint | % { $_.Delete() }
            Remove-Item -Path $workingDirectory -Force -Recurse -Verbose -ErrorAction SilentlyContinue
        }
    }

    function RemoveAllAgents() {
        Write-Output "Removing existing build agents"

        $agentServices = Get-Service -Name "VSTS Agent (tfs.*)"

        if ($null -ne $agentServices) {
            Stop-Service -InputObject $agentServices

            foreach ($agent in $agentServices) {
                & cmd /c "SC DELETE `"$($agent.Name)`""
            }
        }

        if (Test-Path $AgentRootDirectory) {
            $directories = Get-ChildItem -LiteralPath $AgentRootDirectory

            foreach ($directory in $directories) {
                cmd /c "$($directory.FullName)\config.cmd remove --auth Integrated"\
                DeleteRecursive $directory.FullName
            }
        }

        if ($null -ne (Get-Module -Name 'WebAdministration' -ListAvailable)) {
            # Stop IIS while removing build agent directories to prevent file locks
            if (-not (Get-Module -Name 'WebAdministration')) {
                Import-Module 'WebAdministration'
            }

            iisreset.exe /STOP

            # Clear build agent working directory
            [string]$workingDirectory = [System.IO.Path]::Combine($workDirectory, "B")

            # If the path doesn't exist Get-ChildItem will happily pick the working directory instead which could delete C:\Windows\ ...
            # https://github.com/PowerShell/PowerShell/issues/5699
            if (Test-Path $workingDirectory) {
                DeleteRecursive $workingDirectory
            }

            & $PSScriptRoot\iis-cleanup.ps1

            # Start IIS after removing files
            iisreset.exe /START
        }
    }

    function SetHighPower() {
        try  {
            $powerPlan = Get-WmiObject -Namespace root\cimv2\power -Class Win32_PowerPlan -Filter "ElementName = 'High Performance'"
            $powerPlan.Activate()
        } catch {
            Write-Warning $Error[0]
        }
    }

    function ConfigureGit() {
        . $PSScriptRoot\configure-git-for-agent-host.ps1
    }

    function OptimizeBuildEnvironment {
        # TODO: merge with "Optimize-Environment"
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
            "testhost.exe",
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
            "dbprepare.exe",
            "DeploymentEngine.exe",
            "DeploymentManager.exe",
            "Expert.Help.sfx"
            "PackageManagerConsole.exe",
            "Agent.Listener.exe",
            "AgentService.exe",
            "robocopy.exe"
            )

            foreach ($proc in $processes) {
                Add-MpPreference -ExclusionProcess $proc
            }
        } catch {
            Write-Verbose $Error[0].Exception
        }
    }

    function SetupScheduledTasks {
        Write-Information "SetupScheduledTasks"
        $scrptToRun =([System.IO.Path]::Combine($PSScriptRoot, "scheduled-tasks.ps1"))
        . $scrptToRun
    }

    function StopUnneededServices() {
        $services = @(
            "*SQL*OLAP*",
            "*SSASTELEMETRY*",
            "*MsDtsServer*,"
            "DiagTrack")

        foreach ($service in $services) {
            Get-Service -Name $service | Stop-Service
            Get-Service -Name $service | Set-Service –StartupType Manual
        }
    }

    function ProvisionAgent() {
        SetHighPower
        ConfigureGit
        StopUnneededServices

        $agentName = Get-RandomName

        $scratchDirectoryName = Get-Random -Maximum 1024

        $workingDirectory = [System.IO.Path]::Combine($workDirectory, "B", $scratchDirectoryName)

        Write-Output "Agent: $agentName Working directory $workingDirectory"

        $agentInstallationPath = "$AgentRootDirectory\$agentName"

        New-Item -ItemType Directory -Path $AgentRootDirectory -ErrorAction SilentlyContinue

        Expand-Archive $agentArchive -DestinationPath $agentInstallationPath -Force

        try {
            Push-Location -Path $agentInstallationPath

            Write-Output "Installing agent $agentName"

            .\config.cmd --unattended --url $tfsHost --auth Integrated --pool $agentPool --agent $agentName --windowslogonaccount "ADERANT_AP\tfsbuildservice$" --work "$workingDirectory" --replace

            $command = "sc create `"VSTS Agent (tfs.$agentName)`" binpath=$agentInstallationPath\bin\AgentService.exe obj= `"ADERANT_AP\tfsbuildservice$`" start= delayed-auto"
            & cmd /c $command
            net start "VSTS Agent (tfs.$agentName)"
        } finally {
            Pop-Location
        }
    }

    ##
    ## Agent Setup ##
    ##
    if ($removeAllAgents) {
        RemoveAllAgents
    }

    OptimizeBuildEnvironment
    SetupScheduledTasks

    for ($i = 0; $i -lt $agentsToProvision; $i++) {
        ProvisionAgent
    }
}