$currentDirectory = pwd
$psHomeDirectory = Join-Path $Env:UserProfile "Documents\WindowsPowerShell\"
$moduleDirectory = Join-Path $psHomeDirectory "Modules"

if (-not (Test-Path "$moduleDirectory\Aderant")) {	
    New-Item -ItemType Directory -Path $moduleDirectory -Force -ErrorAction SilentlyContinue

    Write-Host "Current script path: $($MyInvocation.MyCommand.Path)"

    $folder = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
    $cmd = "cmd /c mklink " + "$moduleDirectory\Aderant" + " " + $folder + "/D"            
    Invoke-Expression $cmd
            
    # Copy and setup _profile script
    $sourceScript = Join-Path $folder -ChildPath "..\WindowsPowerShell\Microsoft.PowerShell_profile.ps1"
    $destinationScript = Join-Path $psHomeDirectory -ChildPath "Microsoft.PowerShell_profile.ps1"
    Copy-Item $sourceScript $destinationScript 
    Set-ItemProperty $destinationScript -name IsReadOnly -value $false	 
}