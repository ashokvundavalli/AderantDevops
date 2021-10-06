#Requires -RunAsAdministrator

Set-StrictMode -Version 'Latest'

$script:WorkingDirectory = $PSScriptRoot

# Remove legacy tasks
Unregister-ScheduledTask -TaskName "Reclaim Space" -ErrorAction "SilentlyContinue" -Confirm:$false
Unregister-ScheduledTask -TaskName "Remove NuGet Cache" -ErrorAction "SilentlyContinue" -Confirm:$false
Unregister-ScheduledTask -TaskName "Configure Security" -ErrorAction "SilentlyContinue" -Confirm:$false
Unregister-ScheduledTask -TaskName "Cleanup Agent Host" -ErrorAction "SilentlyContinue" -Confirm:$false

# Remove cruft left behind by old scripts
Remove-Item "$script:WorkingDirectory\RestartAgentHostLog.txt" -ErrorAction "SilentlyContinue" -Verbose

function GetScheduledTaskPrincipal() {
    return New-ScheduledTaskPrincipal -UserId "ADERANT_AP\tfsbuildservice$" -LogonType 'Password' -RunLevel 'Highest'
}

function RefreshSetupTask($taskName) {
    $expectedVersion = "2.0"

    $task = Get-ScheduledTask $taskName
    if ($null -eq $task) {
        throw "Task $taskName not found"
    }

    if ($task.Description -eq $expectedVersion) {
        return
    }

    $task.Settings.RestartCount = 3
    $task.Settings.RestartInterval = "PT1M" # 1 minute in TaskXml language https://docs.microsoft.com/en-us/windows/win32/taskschd/tasksettings-restartinterval

    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction 'SilentlyContinue'

    # Scheduled tasks running as a gMSA account at startup require a delay, otherwise they will not execute as they need WinLogon, Kerberos and W32Time to be running.
    $trigger = $task.Triggers[0]
    $trigger.Delay = "PT5M"

    $principal = GetScheduledTaskPrincipal

    # Re-register the scheduled task, this is needed as Set-ScheduledTask unticks the 'Run if not logged in' option
    Register-ScheduledTask -TaskName $taskName -Action $task.Actions[0] -Settings $task.Settings -Trigger $trigger -Principal $principal -Description $expectedVersion -Force
}

<#
============================================================
Reboot Task
============================================================
#>
function Task_RestartAgentHost() {
    $STTrigger = New-ScheduledTaskTrigger -Daily -At 11pm
    $STName = "Restart Agent Host"

    Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue

    $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $PSScriptRoot\restart-agent-host.task.ps1" -WorkingDirectory $script:WorkingDirectory
    $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

    $principal = GetScheduledTaskPrincipal

    Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger –Principal $principal -Settings $STSettings -Force
}

<#
============================================================
Refresh Task
============================================================
#>
function Task_RefreshAgentHostsScripts() {
    $interval = New-TimeSpan -Minutes 15
    $STTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval $interval
    $STName = "Refresh Agent Host Scripts"

    Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue

    $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $PSScriptRoot\refresh-agent-host.task.ps1" -WorkingDirectory $script:WorkingDirectory
    $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

    $principal = GetScheduledTaskPrincipal

    Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger -Principal $principal -Settings $STSettings -Force
}

<#
============================================================
Clean Task
============================================================
#>
function Task_CleanAgentEnvironment() {
    $STName = "Cleanup Agent Environment"

    Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue

    $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $PSScriptRoot\cleanup.task.ps1" -WorkingDirectory $script:WorkingDirectory
    $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

    $principal = GetScheduledTaskPrincipal

    Register-ScheduledTask $STName -Action $STAction -Principal $principal -Settings $STSettings -Force
}

RefreshSetupTask "Setup Agent Host"
Task_RestartAgentHost
Task_RefreshAgentHostsScripts
Task_CleanAgentEnvironment