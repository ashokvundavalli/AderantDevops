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

[string]$defaultParameters = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -File `"$PSScriptRoot\Invoke-MaintenanceScript.ps1`""

$scheduledTaskAction = New-ScheduledTaskAction -Execute "$PSHOME\powershell.exe" -Argument "$defaultParameters $Parameters" -WorkingDirectory (Join-Path -Path $Env:SystemDrive -ChildPath 'Scripts')
$scheduledTaskSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility 'Win8'
$scheduledTaskPrincipal = New-ScheduledTaskPrincipal -UserId $User -LogonType 'Password' -RunLevel 'Highest'

#Register the new scheduled task
Register-ScheduledTask -TaskName $Name -Action $scheduledTaskAction -Settings $scheduledTaskSettings -Trigger $Trigger -Principal $scheduledTaskPrincipal -Force