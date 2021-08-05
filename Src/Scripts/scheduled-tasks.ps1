#Requires -RunAsAdministrator

Set-StrictMode -Version 'Latest'

function WaitForTaskCompletion {
    <#
        .DESCRIPTION
        Waits for the specified amount of time for a scheduled task to enter the 'Ready' state.
        This is to ensure existing scheduled tasks have time to finish running prior to being re-created by this script.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true, HelpMessage='The name of the scheduled task.')][string]$TaskName,
        [Parameter(Mandatory=$false, HelpMessage="The amount of time to wait for the scheduled task to enter the 'Ready' state.")][TimeSpan]$Timeout = [TimeSpan]::FromMinutes(5)
    )

    begin {
        Set-StrictMode -Version 'Latest'
        $InformationPreference = 'Continue'
    }

    process {
        $task = Get-ScheduledTask -TaskName $TaskName -ErrorAction 'SilentlyContinue'

        if ($null -ne $task) {
            [TimeSpan]$waitInterval = [TimeSpan]::FromSeconds(1)

            while ($task.State -notin 'Ready') {
                if ($timeout -eq [TimeSpan]::Zero) {
                    Write-Warning "Timeout waiting for task: $TaskName to enter 'Ready' state."
                    return
                }

                $task = Get-ScheduledTask -TaskName $TaskName

                $timeout = $timeout.Subtract($waitInterval)
            }

            Write-Information -MessageData "Scheduled task: $TaskName is not currently executing."
        }
    }
}

function New-ScheduledMaintenanceTask {
    <#
        .DESCRIPTION
        Creates a scheduled task with built-in logging and alerts.
    #>
    [CmdletBinding()]
    param (
        # The name of the scheduled task.
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Name,

        # The trigger for the scheduled task.
        [Parameter(Mandatory=$true)]
        [System.Object]
        $Trigger,

        # Parameters to pass to the script.
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Parameters,

        # The credentials to set up the scheduled task with.
        [Parameter(Mandatory=$false)]
        [string]
        $User = 'ADERANT_AP\tfsbuildservice$'
    )

    # Remove any existing version of the scheduled task.
    Unregister-ScheduledTask -TaskName $Name -Confirm:$false -ErrorAction 'SilentlyContinue'

    [string]$defaultParameters = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -File `"$PSScriptRoot\Agents\Maintenance\Invoke-MaintenanceScript.ps1`""

    $scheduledTaskAction = New-ScheduledTaskAction -Execute "$PSHOME\powershell.exe" -Argument "$defaultParameters $Parameters" -WorkingDirectory (Join-Path -Path $Env:SystemDrive -ChildPath 'Scripts')
    $scheduledTaskSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility 'Win8'
    $scheduledTaskPrincipal = New-ScheduledTaskPrincipal -UserId $User -LogonType 'Password' -RunLevel 'Highest'

    #Register the new scheduled task
    Register-ScheduledTask -TaskName $Name -Action $scheduledTaskAction -Settings $scheduledTaskSettings -Trigger $Trigger -Principal $scheduledTaskPrincipal -Force
}

#region CreateScheduledTasks
[string]$userName = 'ADERANT_AP\tfsbuildservice$'
# Scheduled tasks running as a gMSA account at startup require a delay, otherwise they will not execute.
$trigger = New-ScheduledTaskTrigger -AtStartup -RandomDelay ([TimeSpan]::FromMinutes(1))

[string]$taskName = 'Reclaim Space'
WaitForTaskCompletion -TaskName $taskName
New-ScheduledMaintenanceTask -Name $taskName -Trigger $trigger -Parameters "-Script `"$PSScriptRoot\make-free-space-vnext.ps1`"" -User $userName
Start-ScheduledTask -TaskName $taskName

[string]$taskName = 'Remove NuGet Cache'
WaitForTaskCompletion -TaskName $taskName
New-ScheduledMaintenanceTask -Name $taskName -Trigger $trigger -Parameters "-Script `"$PSScriptRoot\make-free-space-vnext.ps1`" -Parameters `"-strategy nuget`" -TranscriptName `"RemoveNuGetCache`"" -User $userName
Start-ScheduledTask -TaskName $taskName

[string]$taskName = 'Cleanup Agent Host'
WaitForTaskCompletion -TaskName $taskName
New-ScheduledMaintenanceTask -Name $taskName -Trigger $trigger -Parameters "-Script `"$PSScriptRoot\cleanup-agent-host.ps1`"" -User $userName
Start-ScheduledTask -TaskName $taskName

[string]$taskName = 'Configure Security'
WaitForTaskCompletion -TaskName $taskName
New-ScheduledMaintenanceTask -Name $taskName -Trigger $trigger -Parameters "-Command `". $PSScriptRoot\Agents\Maintenance\Security.ps1;Set-SecurityPermissions;Revoke-SecurityPermissions;`" -TranscriptName `"ConfigureSecurity`"" -User $userName
Start-ScheduledTask -TaskName $taskName
#endregion