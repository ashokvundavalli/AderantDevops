$currentUser = [Security.Principal.WindowsPrincipal]([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdminProcess = $currentUser.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$adminHeader = if ($isAdminProcess) { 'Administrator: ' } else { '' }

# Without this git will look on H:\ for .gitconfig
$Env:HOME = $Env:USERPROFILE

function global:prompt {
    $realLASTEXITCODE = $LASTEXITCODE    

    $location = Get-Location

    Write-Host "PS $(location)" -NoNewline

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

function global:Invoke-Build([switch]$force, [switch]$clean, [switch]$package) {
    $path = $global:CurrentModulePath    

    if ($package) {
        $task = "Package"
    }    

    & $Env:EXPERT_BUILD_DIRECTORY\Build\Invoke-Build.ps1 -Task "$task" -File $Env:EXPERT_BUILD_DIRECTORY\Build\BuildProcess.ps1 -Repository $path -Clean:$clean.ToBool()
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
    
            if (-not (Get-InstalledModule posh-git -ErrorAction SilentlyContinue)) {
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
    try {        
        $result = [bool]::Parse((& git config --get core.autocrlf))
        if ($result) {
            Write-Host (New-Object string -ArgumentList '*', 80) -ForegroundColor Red
            Write-Host "Your git config has autocrlf=true which will cause untold chaos." -ForegroundColor Red
            Write-Host "It will updated to false." -ForegroundColor Red
            Write-Host (New-Object string -ArgumentList '*', 80) -ForegroundColor Red
            sleep -Seconds 10
        }
    } finally {
        # Probably don't have git so we are going to fail hard very soon.
    }

    & git config --global difftool.prompt false
    & git config --global credential.tfs.integrated true
    & git config --global core.autocrlf false

    # set up notepad++ as the default commit editor
    # & git config --global core.editor "'C:/Program Files (x86)/Notepad++/notepad++.exe' -multiInst -notabbar -nosession -noPlugin"
}

InstallPoshGit
ConfigureGit

Export-ModuleMember -Function Invoke-Build
Set-Alias -Name bm -Value Invoke-Build -Scope Global