[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$server,
    [Parameter(Mandatory=$false)][string]$agentPool,
    [switch]$skipAgentDownload,
    [switch]$restart
)

begin {
    Set-StrictMode -Version Latest
}

process {
    if (-not ($server.EndsWith(".ap.aderant.com", "CurrentCultureIgnoreCase"))) {
        $server = "$server.$((gwmi WIN32_ComputerSystem).Domain)"
    }

    [PSCredential]$credentials = Get-Credential "ADERANT_AP\service.tfsbuild.ap"
    $session = New-PSSession -ComputerName $server -Credential $credentials -Authentication Credssp -ErrorAction Stop

    $setupScriptBlock = {
        param (
            [PSCredential]$credentials,
            [bool]$skipDownload
        )

        # Make me fast
        $powerPlan = Get-WmiObject -Namespace root\cimv2\power -Class Win32_PowerPlan -Filter "ElementName = 'High Performance'"
        $powerPlan.Activate()

        # Make me admin
        Add-LocalGroupMember -Group Administrators -Member ADERANT_AP\tfsbuildservice$ -ErrorAction SilentlyContinue
        Add-LocalGroupMember -Group Administrators -Member $credentials.UserName -ErrorAction SilentlyContinue
        Add-LocalGroupMember -Group docker-users -Member ADERANT_AP\tfsbuildservice$ -ErrorAction SilentlyContinue
        Add-LocalGroupMember -Group docker-users -Member $credentials.UserName -ErrorAction SilentlyContinue
        Add-LocalGroupMember -Group Administrators -Member ADERANT_AP\SG_AP_Dev_Operations -ErrorAction SilentlyContinue

        [string]$scriptsDirectory = "$env:SystemDrive\Scripts"

        New-Item -Path $scriptsDirectory -ItemType Directory -ErrorAction SilentlyContinue

        Push-Location $scriptsDirectory

        $credentials | Export-Clixml -Path "$scriptsDirectory\credentials.xml"

        if (-not $skipDownload) {
            Write-Host "Downloading build agent zip"
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            $currentProgressPreference = $ProgressPreference
            $ProgressPreference = "SilentlyContinue"

            try {
                $agentArchive = "$env:SystemDrive\Scripts\vsts-agent.zip"        
                Invoke-WebRequest "https://vstsagentpackage.azureedge.net/agent/2.141.2/vsts-agent-win-x64-2.141.2.zip" -OutFile $agentArchive -UseBasicParsing
            } finally {
                $ProgressPreference = $currentProgressPreference
            }
        }

        Import-Module ServerManager

        if (-not (Get-WindowsFeature | Where-Object {$_.Name -eq "Hyper-V"}).InstallState -eq "Installed") {
            Enable-WindowsOptionalFeature -Online -FeatureName:Microsoft-Hyper-V -All
        }

        # Return the machine specific script home
        return $scriptsDirectory
    }

    [string]$scriptsDirectory = (Invoke-Command -Session $session -ScriptBlock $setupScriptBlock -ArgumentList $credentials, $skipAgentDownload.IsPresent)[1]

    Write-Host "Generating Scheduled Tasks"

    <# 
    ============================================================
    Setup Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        param (
            [string]$scriptsDirectory,
            $credentials,
            [string]$agentPool
        )

        if (-not [string]::IsNullOrWhiteSpace($agentPool)) {
            Write-Host "Setting agent pool: $agentPool"
            [Environment]::SetEnvironmentVariable('AgentPool', $agentPool, 'Machine')
        }

        $STTrigger = New-ScheduledTaskTrigger -AtStartup
        [string]$STName = "Setup Agent Host"
        
        Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue
        
        #Action to run as
        $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $scriptsDirectory\Build.Infrastructure\Src\Scripts\setup-agent-host.ps1" -WorkingDirectory $scriptsDirectory
        #Configure when to stop the task and how long it can run for. In this example it does not stop on idle and uses the maximum possible duration by setting a timelimit of 0
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

        #Register the new scheduled task
        Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger -User $credentials.UserName -Password $credentials.GetNetworkCredential().Password -Settings $STSettings -RunLevel Highest -Force
    } -ArgumentList $scriptsDirectory, $credentials, $agentPool


    <# 
    ============================================================
    Clean up agent Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        $STTrigger = New-ScheduledTaskTrigger -AtStartup
        [string]$STName = "Cleanup Agent Host"
        
        Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue
        
        #Action to run as
        $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $scriptsDirectory\Build.Infrastructure\Src\Scripts\cleanup-agent-host.ps1" -WorkingDirectory $scriptsDirectory        
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

        #Register the new scheduled task
        Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger -User $credentials.UserName -Password $credentials.GetNetworkCredential().Password -Settings $STSettings -RunLevel Highest -Force
    } -ArgumentList $scriptsDirectory, $credentials


    <# 
    ============================================================
    Reboot Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        $STTrigger = New-ScheduledTaskTrigger -Daily -At 11pm
        [string]$STName = "Restart Agent Host"
        
        Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue
        
        #Action to run as
        $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $scriptsDirectory\Build.Infrastructure\Src\Scripts\restart-agent-host.ps1" -WorkingDirectory $scriptsDirectory
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

        $principal = New-ScheduledTaskPrincipal -UserID tfsbuildservice$ -LogonType Password -RunLevel Highest
        #Register the new scheduled task
        Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger –Principal $principal -Settings $STSettings -Force          
     } -ArgumentList $scriptsDirectory


    <# 
    ============================================================
    Refresh Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        $interval = New-TimeSpan -Minutes 15
        $STTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval $interval
        [string]$STName = "Refresh Agent Host Scripts"
        
        Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue
        
        #Action to run as
        $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $scriptsDirectory\Build.Infrastructure\Src\Scripts\refresh-agent-host.ps1" -WorkingDirectory $scriptsDirectory        
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

        $principal = New-ScheduledTaskPrincipal -UserID tfsbuildservice$ -LogonType Password -RunLevel Highest
        #Register the new scheduled task
        Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger –Principal $principal -Settings $STSettings -Force
     } -ArgumentList $scriptsDirectory


    <# 
    ============================================================
    Docker Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        [string]$docker = "$env:ProgramFiles\Docker\Docker\Docker for Windows.exe"

        if (Test-Path $docker) {
            if ((Get-WmiObject -Class Win32_Service -Filter "Name='docker'") -eq $null) {
                Write-Host "Registering Docker service"
                & "$env:ProgramFiles\Docker\Docker\resources\dockerd.exe" --register-service
            }

            $interval = New-TimeSpan -Minutes 1
            $STTrigger = New-ScheduledTaskTrigger -AtStartup -RandomDelay ($interval)
            
            [string]$STName = "Run Docker for Windows"
            Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue

            #Action to run as
            $STAction = New-ScheduledTaskAction -Execute $docker
            $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

            #Register the new scheduled task
            Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger -User $credentials.UserName -Password $credentials.GetNetworkCredential().Password -Settings $STSettings -RunLevel Highest -Force
        }
    } -ArgumentList $scriptsDirectory, $credentials

    <#
    ============================================================
    Cleanup space Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        $interval = New-TimeSpan -Minutes 5
        $STTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval $interval
        [string]$STName = "Reclaim Space"
        
        Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue
        
        # Action to run as
        $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $scriptsDirectory\Build.Infrastructure\Src\Scripts\make-free-space-vnext.ps1" -WorkingDirectory $scriptsDirectory        
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

        $principal = New-ScheduledTaskPrincipal -UserID tfsbuildservice$ -LogonType Password -RunLevel Highest
        # Register the new scheduled task
        Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger –Principal $principal -Settings $STSettings -Force 
    } -ArgumentList $scriptsDirectory

    <# 
    ============================================================
    Cleanup NuGet Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        $interval = New-TimeSpan -Hours 2
        $STTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval $interval
        [string]$STName = "Remove NuGet Cache"
        
        Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue
        
        # Action to run as
        $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $scriptsDirectory\Build.Infrastructure\Src\Scripts\make-free-space-vnext.ps1 -strategy nuget" -WorkingDirectory $scriptsDirectory        
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

        $principal = New-ScheduledTaskPrincipal -UserID tfsbuildservice$ -LogonType Password -RunLevel Highest
        # Register the new scheduled task
        Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger –Principal $principal -Settings $STSettings -Force 
    } -ArgumentList $scriptsDirectory

    Invoke-Command -Session $session -ScriptBlock {
        Remove-Item "$scriptsDirectory\Build.Infrastructure" -Force -Recurse -ErrorAction SilentlyContinue
        & git clone "http://tfs.ap.aderant.com:8080/tfs/ADERANT/ExpertSuite/_git/Build.Infrastructure" "$scriptsDirectory\Build.Infrastructure" -q
    } -ArgumentList $scriptsDirectory, $credentials

    if ($restart.IsPresent) {
        Write-Host "Restarting build agent: $($server)"
        shutdown.exe /r /f /t 0 /m \\$server /d P:4:1
    }
}