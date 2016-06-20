# Get the ID and security principal of the current user account
$myWindowsId = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$myWindowsPrincipal = new-object System.Security.Principal.WindowsPrincipal($myWindowsId) 

# Get the security principal for the Administrator role
$adminRole=[System.Security.Principal.WindowsBuiltInRole]::Administrator 

# Check to see if we are currently running "as Administrator"
if ($myWindowsPrincipal.IsInRole($adminRole)) {

   # We are running "as Administrator" - so change the title and background color to indicate this
   $Host.UI.RawUI.WindowTitle = $myInvocation.MyCommand.Definition + "(Elevated)"
   $Host.UI.RawUI.BackgroundColor = "DarkBlue"
   clear-host
} else {

   # We are not running "as Administrator" - so relaunch as administrator
   # Create a new process object that starts PowerShell
   $newProcess = new-object System.Diagnostics.ProcessStartInfo "PowerShell"

   # Specify the current script path and name as a parameter
   $newProcess.Arguments = $myInvocation.MyCommand.Definition

   # Indicate that the process should be elevated
   $newProcess.Verb = "runas"

   # Start the new process
   [System.Diagnostics.Process]::Start($newProcess)
   # Exit from the current, unelevated, process
   exit
}

try {
    $ErrorActionPreference = 'Stop'

    $psHomeDirectory = Join-Path $Env:UserProfile "Documents\WindowsPowerShell\"
    $moduleDirectory = Join-Path $psHomeDirectory "Modules"

    if (Test-Path "$moduleDirectory\Aderant") {
        $cmd = "cmd /c rmdir " + $moduleDirectory + "\Aderant /q /s"
        Invoke-Expression $cmd
    }

    # Copy and setup _profile script
    $sourceScript = Join-Path $PSScriptRoot -ChildPath "..\WindowsPowerShell\Microsoft.PowerShell_profile.ps1"
    $destinationScript = Join-Path $psHomeDirectory -ChildPath "Microsoft.PowerShell_profile.ps1"

    if (-not (Test-Path $sourceScript)) {
        throw "Cannot find Microsoft.PowerShell_profile.ps1"
        return
    }

    $scriptText = Get-Content -Raw $sourceScript
    $scriptText = $scriptText -f $PSScriptRoot | Out-File $destinationScript

    Write-Host -NoNewLine "Press any key to continue..."
} catch {
    Write-Host "An exception occured during profile setup. Sorry about that."

    Write-Host $_
    Read-Host -Prompt "Press any key to continue"
}


