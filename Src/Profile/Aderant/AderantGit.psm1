$currentUser = [Security.Principal.WindowsPrincipal]([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdminProcess = $currentUser.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$adminHeader = if ($isAdminProcess) { 'Administrator: ' } else { '' }

function global:prompt {
    $realLASTEXITCODE = $LASTEXITCODE    

    $location = Get-Location

    Write-Host "PS $(location)"  -NoNewline

    #TODO: Remove global variables        
    WriteRepositoryInfo $location        

    if ((Get-Module posh-git) -ne $null) {
        Write-VcsStatus        
    }   
     
    Write-Host  "$('>' * ($nestedPromptLevel + 1))" -NoNewline
    
    $global:LASTEXITCODE = $realLASTEXITCODE
        
    # Default console looks like this
    # PS C:\WINDOWS\system32> 
    return " "
}

function global:Invoke-Build([switch]$force) {
    $path = $global:CurrentModulePath

    $buildFile = [System.IO.FileInfo]([System.IO.Path]::Combine($path, "Build.ps1"))
    if (-not ($buildFile.Exists)) {
        InitializeRepository $path
    } else {
        ValidateRepository $path $force
    }
}

function WriteRepositoryInfo([string]$location) {
    $Host.UI.RawUI.WindowTitle = "{0} [{1}]" -f $adminHeader, $global:CurrentModuleName       

    $path = $global:CurrentModulePath.TrimEnd('\')

    # As a reminder, write the repository hint if we are outside of the directory 
    if (-not $location.Contains($path)) {
        Write-Host " [" -ForegroundColor Yellow -NoNewline
        Write-Host $global:CurrentModuleName -ForegroundColor DarkCyan -NoNewline 
        Write-Host "]" -ForegroundColor Yellow -NoNewline
    }
}

function ValidateRepository([string]$path, [bool]$force) {
    $actualHash = Get-FileHash "$path\Build.ps1"
    $expectedHash = Get-FileHash "$ShellContext.BuildScriptsDirectory\Build.ps1"

    if ($actualHash.Hash -ne $expectedHash.Hash) {
        if (-not $force) {
            Write-Host "Build.ps1 is out of date. Specify [force] switch to update it." -ForegroundColor Yellow
            return
        }

        Write-Host "Build.ps1 is out of date. Updating it." -ForegroundColor Yellow
        InitializeRepository $path
    }
}

function InitializeRepository([string]$path) {
    Copy-Item $ShellContext.BuildScriptsDirectory\Build.ps1 -Destination $path\Build.ps1 -Force
}

function InstallPoshGit() {
    # We need Windows 10 or WMF 5 for Install-Module
    if ($host.Version.Major -ge 5) {
    
        if (-not (Get-InstalledModule posh-git)) {
            Install-Module posh-git
        }
        Import-Module posh-git -Global
    } else {
        Write-Host "You do not have Windows 10 or PowerShell 5. Windows 10 provides a much improved PowerShell experience." -ForegroundColor Yellow
    }
}

InstallPoshGit

Export-ModuleMember -Function Invoke-Build
Set-Alias -Name bm -Value Invoke-Build -Scope Global