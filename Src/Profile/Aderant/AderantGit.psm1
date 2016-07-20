$currentUser = [Security.Principal.WindowsPrincipal]([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdminProcess = $currentUser.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$adminHeader = if ($isAdminProcess) { 'Administrator: ' } else { '' }

# Without this git will look on H:\ for .gitconfig
$Env:HOME = $Env:USERPROFILE

function global:prompt {
    $realLASTEXITCODE = $LASTEXITCODE    

    $location = Get-Location

    Write-Host "PS $(location)" -NoNewline

    #TODO: Remove global variables        
    WriteRepositoryInfo $location        

    if ($ShellContext.PoshGitAvailable) {
        Write-VcsStatus

        $status = Get-GitStatus

        if ($status -ne $null) {    
            $repoName = Split-Path -Leaf (Split-Path $status.GitDir)    
            $Host.UI.RawUI.WindowTitle = "$script:adminHeader$repoName [$($status.Branch)]"
        }
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

    . $buildFile
}

function WriteRepositoryInfo([string]$location) {
    $path = $global:CurrentModulePath.TrimEnd('\')

    # As a reminder, write the repository hint if we are outside of the directory 
    if (-not $location.Contains($path)) {
        Write-Host " [" -ForegroundColor Yellow -NoNewline
        Write-Host $global:CurrentModuleName -ForegroundColor DarkCyan -NoNewline 
        Write-Host "]" -ForegroundColor Yellow -NoNewline
    }
}

function ValidateRepository([string]$path, [bool]$force) {
    $actualHash = Get-FileHash (Join-Path $path -ChildPath Build.ps1)
    $expectedHash = Get-FileHash (Join-Path $ShellContext.BuildScriptsDirectory -ChildPath Build.ps1)

    if ($actualHash.Hash -ne $expectedHash.Hash) {
        if (-not $force) {
            #Write-Host "Build.ps1 is out of date. Specify [force] switch to update it." -ForegroundColor Yellow
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

        try {
            # Optimization, Get-InstalledModule is quite slow so just peek directly
            if (Test-Path $Env:ProgramFiles\WindowsPowerShell\Modules\posh-git) {
                Import-Module posh-git -Global
                return
            }
    
            if (-not (Get-InstalledModule posh-git)) {
                Install-Module posh-git
            }            
        } finally {
            Import-Module posh-git -Global            
            $global:GitPromptSettings.EnableWindowTitle = $false            
            $ShellContext.PoshGitAvailable = (Get-Module posh-git) -ne $null
        }
    } else {
        Write-Host "You do not have Windows 10 or PowerShell 5. Windows 10 provides a much improved PowerShell experience." -ForegroundColor Yellow
    }
}

function ConfigureGit() {
    & git config --global --add difftool.prompt false
    & git config --global credential.tfs.integrated true
}

InstallPoshGit
ConfigureGit

Export-ModuleMember -Function Invoke-Build
Set-Alias -Name bm -Value Invoke-Build -Scope Global