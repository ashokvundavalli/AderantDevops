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
   $newProcess.Arguments = "-noprofile -file $PSCommandPath"

   # Indicate that the process should be elevated
   $newProcess.Verb = "runas"

   # Start the new process
   [System.Diagnostics.Process]::Start($newProcess) | Out-Null
   # Exit from the current, unelevated, process
   exit
}

try {
    $ErrorActionPreference = 'Stop'

    $currentDirectory = pwd
    $psHomeDirectory = Join-Path $Env:UserProfile "Documents\WindowsPowerShell\"
    $moduleDirectory = Join-Path $psHomeDirectory "Modules"

    if (Test-Path "$moduleDirectory\Aderant") {
        $cmd = "cmd /c rmdir " + $moduleDirectory + "\Aderant /q /s"
        Invoke-Expression $cmd
    }

    if (-not (Test-Path "$moduleDirectory\Aderant")) {
        New-Item -ItemType Directory -Path $moduleDirectory -Force -ErrorAction SilentlyContinue | Out-Null

        Write-Debug "Current script path: $($MyInvocation.MyCommand.Path)"

        $folder = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
            
        # Copy and setup _profile script
        $sourceScript = Join-Path $folder -ChildPath "..\WindowsPowerShell\Microsoft.PowerShell_profile.ps1"
        $destinationScript = Join-Path $psHomeDirectory -ChildPath "Microsoft.PowerShell_profile.ps1"
    
        $text = Get-Content -Raw $sourceScript
        $text = $text -replace "PROFILE_PATH",$folder
    
        if (Test-Path $destinationScript) {
            Write-Host "***" -ForegroundColor Yellow
            Write-Host "!!! A Microsoft.PowerShell_profile.ps1 script already exists. If this script has modifications those modifications will be lost." -ForegroundColor Yellow
            Write-Host "***" -ForegroundColor Yellow
            $text | Out-File $destinationScript -Confirm
        } else {
            $text | Out-File $destinationScript
        }
                
        Set-ItemProperty $destinationScript -name IsReadOnly -value $false
        Write-Host "Setup complete."
    }
} catch {
    Write-Host "An exception occured during profile setup. Sorry about that."

    Write-Host $_
    Read-Host -Prompt "Press any key to continue"
}