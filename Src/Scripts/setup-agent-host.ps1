#Requires -RunAsAdministrator

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [int]
    $agentsToProvision = 1,

    [Parameter(Mandatory=$false)]
    [bool]
    $removeAllAgents = $true,

    [Parameter(Mandatory=$false)]
    [ValidateNotNullOrEmpty()]
    [string]
    $agentArchive,

    [Parameter(Mandatory=$false)]
    [ValidateNotNullOrEmpty()]
    [string]
    $tfsHost = "https://tfs.aderant.com/tfs",

    [Parameter(Mandatory=$false)]
    [ValidateNotNullOrEmpty()]
    [ValidateSet('Default'
                 , 'Database'
                 , 'DevOps-Container'
                 , 'DevOps-Test'
                 , 'Family'
                 , 'Mac - Experimental'
                 , 'OSX'
                 , 'Test'
                 , 'Test-WPF'
                 , 'WPF-POC')]
    [string]
    $agentPool,

    [Parameter(Mandatory=$false)]
    [ValidateNotNullOrEmpty()]
    [ValidatePattern('^.*\\.*$')]
    [string]
    $serviceAccount = 'ADERANT_AP\tfsbuildservice$',

    [Parameter(Mandatory=$false)]
    [ValidateNotNull()]
    [string]
    $serviceAccountPassword = $null,

    [Parameter(HelpMessage='Auto Logon is incompatible with gMSA accounts.')]
    [switch]$enableAutoLogon
)

begin {
    Set-StrictMode -Version 'Latest'
    $InformationPreference = 'Continue'
    $ErrorActionPreference = 'Stop'
    $VerbosePreference = 'Continue'
    $ProgressPreference = 'SilentlyContinue'

    [string]$AgentRootDirectory = "$Env:SystemDrive\Agents"

    function Get-RandomName {
        Write-Information -MessageData 'Generating an agent name.'

        # Based on https://github.com/docker/docker/blob/master/pkg/namesgenerator/names-generator.go
        [string]$agentPrefixes = "$PSScriptRoot\Agents\Prefixes.txt"
        [string]$agentSuffixes = "$PSScriptRoot\Agents\Suffixes.txt"

        if (-not (Test-Path -Path $agentPrefixes)) {
            Write-Error "Failed to generate agent name. Could not find agent prefixes at: '$agentPrefixes'."
        }

        if (-not (Test-Path -Path $agentSuffixes)) {
            Write-Error "Failed to generate agent name. Could not find agent prefixes at: '$agentSuffixes'."
        }

        Write-Verbose -Message 'Reading from suffix and prefix lists.'
        [string[]]$prefixes = [System.IO.File]::ReadAllLines($agentPrefixes)
        [string[]]$suffixes = [System.IO.File]::ReadAllLines($agentSuffixes)

        Write-Verbose -Message 'Getting random prefix and suffix.'
        $prefixesRnd = Get-Random -Minimum 0 -Maximum $prefixes.Length
        $suffixesRnd = Get-Random -Minimum 0 -Maximum $suffixes.Length

        $agentName = "$($env:COMPUTERNAME)_{0}_{1}" -f ($prefixes[$prefixesRnd], $suffixes[$suffixesRnd])
        Write-Verbose -Message ('Generated name {0}.' -f $agentName)

        Write-Information -MessageData 'Finished generating agent name.'
        return $agentName
    }

    function DeleteRecursive([string] $workingDirectory) {
        # Work around PowerShell bugs: https://github.com/powershell/powershell/issues/621
        if (Test-Path $workingDirectory) {
            Get-ChildItem -LiteralPath $workingDirectory -Recurse -Attributes ReparsePoint | ForEach-Object { $_.Delete() }
            Remove-Item -Path $workingDirectory -Force -Recurse -Verbose -ErrorAction SilentlyContinue
        }
    }

    function RemoveServices {
        Write-Information -MessageData "Removing existing agent services"

        $agentNamePattern = "VSTS Agent (tfs.*)"
        Write-Verbose -Message "Getting services matching $agentNamePattern"
        $agentServices = Get-Service -Name $agentNamePattern
        if ($null -ne $agentServices) {
            Write-Verbose -Message "Stopping service: $agentServices"
            Stop-Service -InputObject $agentServices

            foreach ($agent in $agentServices) {
                Write-Verbose -Message "Deleting service: $($agent.Name)"
                & cmd /c "SC DELETE `"$($agent.Name)`""
            }
        } else {
            Write-Verbose -Message "No services found matching '$agentNamePattern'. Nothing to stop or remove."
        }
    }

    function RemoveAgentWorkingDirectory {
        Write-Information -MessageData "Removing the agent Working directory at $AgentRootDirectory"
        if (Test-Path $AgentRootDirectory) {
            $directories = Get-ChildItem -LiteralPath $AgentRootDirectory -Directory

            foreach ($directory in $directories) {
                Write-Verbose -Message "Removing agent directory `"$directory`" from config.cmd"
                cmd /c "`"$($directory.FullName)\config.cmd`" remove --auth Integrated"\
                DeleteRecursive $directory.FullName
            }
        }

        if ($null -ne (Get-Module -Name 'WebAdministration' -ListAvailable)) {
            # Stop IIS while removing build agent directories to prevent file locks
            Write-Verbose -Message "Stopping IIS while removing build agent directories to prevent file locks."
            iisreset.exe /STOP
            try {
                # Clear build agent working directory
                [string]$workingDirectory = [System.IO.Path]::Combine("$Env:SystemDrive\", 'B')

                # If the path doesn't exist Get-ChildItem will happily pick the working directory instead which could delete C:\Windows\ ...
                # https://github.com/PowerShell/PowerShell/issues/5699
                if (Test-Path $workingDirectory) {
                    Write-Verbose -Message "Deleting $workingDirectory recursively."
                    DeleteRecursive $workingDirectory
                } else {
                    Write-Verbose -Message "Unable to find $workingDirectory. Nothing to remove."
                }

                & $PSScriptRoot\iis-cleanup.ps1
            } finally {
                # Start IIS after removing files
                Write-Verbose -Message "The working directory should be removed. Restarting IIS."
                iisreset.exe /START
            }
        }
    }

    function RemoveAllAgents() {
        Write-Information -MessageData "Removing all existing build agents"
        RemoveServices
        RemoveAgentWorkingDirectory
    }

    function SetHighPower() {
        Write-Information -MessageData "Setting power options to high performance."
        try  {
            $powerPlan = Get-WmiObject -Namespace root\cimv2\power -Class Win32_PowerPlan -Filter "ElementName = 'High Performance'"
            $powerPlan.Activate()
        } catch {
            Write-Warning $Error[0]
        }
    }

    function ConfigureGit() {
        Write-Information -MessageData 'Configuring Git for agent host'
        . $PSScriptRoot\configure-git-for-agent-host.ps1
    }

    function SetupScheduledTasks {
        Write-Information -MessageData "Setting up scheduled tasks."
        & "$PSScriptRoot\scheduled-tasks.ps1"
    }

    function StopUnneededServices() {
        Write-Information -MessageData "Stopping unrequired services."
        $services = @(
            "*SQL*OLAP*",
            "*SSASTELEMETRY*",
            "*MsDtsServer*,"
            "DiagTrack")

        foreach ($service in $services) {
            Write-Verbose -Message "Stopping $service and setting its start up type to manual."
            Get-Service -Name $service | Stop-Service
            Get-Service -Name $service | Set-Service –StartupType Manual
        }
    }

    function Get-AgentWorkingDirectory {
        $scratchDirectoryName = Get-Random -Maximum 1024
        $workingDirectory = [System.IO.Path]::Combine("$Env:SystemDrive\", 'B', $scratchDirectoryName)
        Write-Information -MessageData "Agent: $agentName Working directory $workingDirectory"

        return $workingDirectory
    }

    function Get-AgentPool {
        if (-not [string]::IsNullOrWhiteSpace($Env:AgentPool)) {
            return $Env:AgentPool
        } else {
            return 'Default'
        }
    }

    function ProvisionAgent() {
        if ($enableAutoLogon.IsPresent -and [string]::IsNullOrEmpty($serviceAccountPassword)) {
            Write-Error -Message "Unable to configure auto logon for service account: '$serviceAccount'. A password must be specified."
        }

        Write-Information -MessageData "Provisioning the build agent."
        SetHighPower
        ConfigureGit
        StopUnneededServices

        # Defaulting as required
        $agentName = Get-RandomName
        $workingDirectory = Get-AgentWorkingDirectory
        New-Item -ItemType Directory -Path $AgentRootDirectory -ErrorAction 'SilentlyContinue'
        $agentInstallationPath = "$AgentRootDirectory\$agentName"

        if ([string]::IsNullOrWhiteSpace($agentPool)) {
            $agentPool = Get-AgentPool
        }

        Expand-Archive $agentArchive -DestinationPath $agentInstallationPath -Force

        try {
            Push-Location -Path $agentInstallationPath

            Write-Information -MessageData "Configuiring agent $agentName"
            Write-Verbose -Message "Agent Name: $agentName"
            Write-Verbose -Message "Agent Pool: $agentPool"
            Write-Verbose -Message "Working Directory: $workingDirectory"
            Write-Verbose -Message "Agent Install Path: $agentInstallationPath"
            Write-Verbose -Message "TFS Host: $tfsHost"
            Write-Verbose -Message "serviceAccount: $serviceAccount"

            # Configure machine for running UI tests.
            if ($enableAutoLogon.IsPresent) {
                # Agent cannot be run as a service for UI tests. See: https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/agents?view=azure-devops&tabs=browser#interactive-or-service
                .\config.cmd --unattended --url $tfsHost --auth Integrated --pool $agentPool --agent $agentName --work "$workingDirectory" --replace

                [string[]]$parts = $serviceAccount.Split('\')

                [Hashtable]$parameters = @{
                    'DefaultUserName' = $parts[1];
                    'DefaultDomainName' = $parts[0];
                }

                if (-not [string]::IsNullOrEmpty($serviceAccountPassword)) {
                    $parameters.Add('DefaultPassword', (ConvertTo-SecureString -String $serviceAccountPassword -AsPlainText -Force))
                }

                & "$PSScriptRoot\Agents\Configuration\ConfigureAgentForWPFTests.ps1" @parameters

                # Remove existing VSTS Agent scheduled task
                [string]$scheduledTaskName = 'VSTS Agent'
                Unregister-ScheduledTask -TaskName $scheduledTaskName -Confirm:$false -ErrorAction 'SilentlyContinue'

                # Add new VSTS Agent scheduled task
                $action = New-ScheduledTaskAction -Execute "$agentInstallationPath\run.cmd"
                $trigger = New-ScheduledTaskTrigger -AtLogOn
                Register-ScheduledTask -Action $action -Trigger $trigger -TaskName $scheduledTaskName -Description "Runs the VSTS agent on service account logon." -RunLevel 'Highest' -User $serviceAccount

                # Start the scheduled task.
                Get-ScheduledTask -TaskName $scheduledTaskName | Start-ScheduledTask

                return
            }

            .\config.cmd --unattended --url $tfsHost --auth Integrated --pool $agentPool --agent $agentName --windowslogonaccount $serviceAccount --work "$workingDirectory" --replace

            [string]$serviceName = "VSTS Agent (tfs.$agentName)"
            # Create our arguments to create a service.
            [string[]]$arguments = @(
                'create',
                "`"$serviceName`"",
                "binpath= `"$agentInstallationPath\bin\AgentService.exe`"",
                "obj= `"$serviceAccount`"",
                'start= delayed-auto'
            )

            if (-not [string]::IsNullOrEmpty($serviceAccountPassword)) {
                $arguments += "password= `"$serviceAccountPassword`""
            }

            Write-Verbose -Message "Arguments:"
            $arguments | ForEach-Object {
                if ($_.StartsWith('password= ')) {
                    Write-Verbose -Message ([string]::Concat($_.Substring(0, 10), [string]::new('*', 5)))
                } else {
                    Write-Verbose -Message "  $_"
                }
            }

            $process = $null

            try {
                Write-Verbose -Message "Creating the agent service `"$serviceName`""
                $process = Start-Process -FilePath 'sc.exe' -ArgumentList $arguments -PassThru -NoNewWindow -Wait
                [int]$exitCode = $process.ExitCode
                if ($exitCode -ne 0) {
                    Write-Error -Message "sc.exe exited with code: $exitCode."
                }
            } finally {
                if ($null -ne $process) {
                    $process.Dispose()
                    $process = $null
                }
            }

            Write-Verbose -Message "Starting $serviceName"
            Start-Service -Name $serviceName
        } finally {
            Pop-Location
        }
    }

    function Get-RandomNameComponent {
        Param (
            [string]
            [ValidateNotNullOrEmpty()]
            $nameFile
        )

        [string[]]$names = [System.IO.File]::ReadAllLines($nameFile)
        [int]$RandomSelectedNameIndex = Get-Random -Minimum 0 -Maximum $names.Length

        return $names[$RandomSelectedNameIndex]
    }

    function Set-AgentsToProvision {
        if ($agentsToProvision -lt 1) {
            Write-Verbose -Message "Defaulting agentsToProvision."
            $originalValue = $agentsToProvision
            if ((Get-CimInstance win32_computersystem).Model -eq "Virtual Machine") {
                $agentsToProvision = 1
            } else {
                $agentsToProvision = ((Get-CimInstance -Class win32_Processor).NumberOfCores / 2)
                if ($agentsToProvision -eq 0) {
                    $agentsToProvision = 1
                }
            }

            Write-Verbose -Message "Defaulted agentsToProvision from {$originalValue} to {$agentsToProvision}."
        }
    }

    function Set-AgentPool {
        if ([string]::IsNullOrWhiteSpace($agentPool)) {
            Write-Verbose -Message "Defaulting AgentPool."
            if (-not [string]::IsNullOrWhiteSpace($Env:AgentPool)) {
                $agentPool = $Env:AgentPool
            } else {
                $agentPool = "Default"
            }
            Write-Verbose -Message "Defaulted agentPool to $agentPool."
        }
    }
}

process {
    try {
        Start-Transcript -Path "$Env:SystemDrive\Scripts\SetupAgentHostLog.txt" -Force
        # Defaulting
        Set-AgentsToProvision
        Set-AgentPool

        if ([string]::IsNullOrWhiteSpace($agentArchive)) {
            [string]$agentArchive = "$Env:SystemDrive\Scripts\vsts-agent.zip"
            [string]$vsTestAgentUrl = "https://vstsagentpackage.azureedge.net/agent/2.141.2/vsts-agent-win-x64-2.141.2.zip"

            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Write-Verbose -Message "Downloading test agent from: '$vsTestAgentUrl' to: '$agentArchive'."
            Invoke-WebRequest $vsTestAgentUrl -OutFile $agentArchive
        }

        # Agent Setup
        if ($removeAllAgents) {
            RemoveAllAgents
        }

        SetupScheduledTasks

        for ($i = 0; $i -lt $agentsToProvision; $i++) {
            ProvisionAgent
        }

        . $PSScriptRoot\configure-disk-device-parameters.ps1
        . $PSScriptRoot\optimize-drives.ps1
        . $PSScriptRoot\Disable-InternetExplorerESC.ps1
    } finally {
        Stop-Transcript
    }
}