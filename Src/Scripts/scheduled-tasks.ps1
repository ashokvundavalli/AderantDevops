[string]$powerShell = "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
[string]$userName = 'ADERANT_AP\tfsbuildservice$'
$principal = New-ScheduledTaskPrincipal -UserID $userName -LogonType Password -RunLevel Highest

& {
    <#
    ============================================================
    Cleanup space Task
    ============================================================
    #>
    $STTrigger = New-ScheduledTaskTrigger -AtStartup
    [string]$STName = "Reclaim Space"

    Unregister-ScheduledTask -TaskName $STName -Confirm:$false -Verbose -ErrorAction SilentlyContinue

    # Action to run as
    $STAction = New-ScheduledTaskAction -Execute $powerShell -Argument "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $PSScriptRoot\make-free-space-vnext.ps1" -WorkingDirectory $PSScriptRoot
    $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

    # Register the new scheduled task
    Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger -Principal $principal -Settings $STSettings -Verbose -Force
}

& {
    <#
    ============================================================
    Cleanup NuGet Task
    ============================================================
    #>
    $STTrigger = New-ScheduledTaskTrigger -AtStartup
    [string]$STName = "Remove NuGet Cache"

    Unregister-ScheduledTask -TaskName $STName -Confirm:$false -Verbose -ErrorAction SilentlyContinue

    $STAction = New-ScheduledTaskAction -Execute $powerShell -Argument "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $PSScriptRoot\make-free-space-vnext.ps1 -strategy nuget" -WorkingDirectory $PSScriptRoot
    $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

    Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger -Principal $principal -Settings $STSettings -Verbose -Force
}

& {
    <#
    ============================================================
    Clean up agent Task
    ============================================================
    #>
    $STTrigger = New-ScheduledTaskTrigger -AtStartup
    [string]$STName = "Cleanup Agent Host"

    Unregister-ScheduledTask -TaskName $STName -Confirm:$false -Verbose -ErrorAction SilentlyContinue

    $STAction = New-ScheduledTaskAction -Execute $powerShell -Argument "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $PSScriptRoot\cleanup-agent-host.ps1" -WorkingDirectory $PSScriptRoot
    $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

    Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger -Principal $principal -Settings $STSettings -Verbose -Force
}

# Configure Security Task
& "$PSScriptRoot\Agents\Maintenance\New-ScheduledTask.ps1" -Name 'Configure Security' -Trigger (New-ScheduledTaskTrigger -AtStartup) -Parameters "-Command `". $PSScriptRoot\Agents\Maintenance\Security.ps1;Set-SecurityPermissions;Revoke-SecurityPermissions;`" -TranscriptName `"ConfigureSecurity`"" -User $userName