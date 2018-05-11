$currentUser = [Security.Principal.WindowsPrincipal]([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdminProcess = $currentUser.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$adminHeader = if ($isAdminProcess) { 'Administrator: ' } else { '' }

function global:Enable-GitPrompt{
    Function global:prompt {
        $realLASTEXITCODE = $LASTEXITCODE    
    
        $location = Get-Location
    
        Write-Host("")
        Write-Host ("Module [") -nonewline
        Write-Host ($global:CurrentModuleName) -nonewline -foregroundcolor DarkCyan
        Write-Host ("] at [") -nonewline
        Write-Host ($global:CurrentModulePath) -nonewline -foregroundcolor DarkCyan
        Write-Host ("]")
    
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
}


function global:Invoke-Build() {
    param (
        [switch]$force,
        [switch]$clean,
        [switch]$package,
        [switch]$debug,
        [switch]$release,
        [bool]$codeCoverage = $true,
        [switch]$integration,
        [switch]$automation,
        [switch]$codeCoverageReport
    )

    begin {
        Set-StrictMode -Version Latest
    }

    process {
        if ($debug -and $release) {
            Write-Error "You can specify either -debug or -release but not both."
            return
        }
        $flavor = ""
        if ($debug) {
            $flavor = "Debug"
            Write-Host "Forcing BuildFlavor to be DEBUG" -ForegroundColor DarkGreen
        } elseif ($release) {
            $flavor = "Release"
            Write-Host "Forcing BuildFlavor to be RELEASE" -ForegroundColor DarkGreen
        }

        $repositoryPath = $global:CurrentModulePath

        [string]$task = ""
        
        [bool]$skipPackage = $false

        if ((Test-Path "$repositoryPath\.git") -eq $false) {
            $skipPackage = ([System.IO.Directory]::GetFiles($repositoryPath, "*.paket.template", [System.IO.SearchOption]::TopDirectoryOnly)).Length -eq 0
        }

        if ($package -and -not $skipPackage) {
            $task = "Package"
        }

        if ($skipPackage) {
            & $Env:EXPERT_BUILD_DIRECTORY\Build\Invoke-Build.ps1 -Task "$task" -File $Env:EXPERT_BUILD_DIRECTORY\Build\BuildProcess.ps1 -Repository $repositoryPath -Clean:$clean.ToBool() -Flavor:$flavor -CodeCoverage $codeCoverage -Integration:$integration.ToBool() -Automation:$automation.ToBool() -SkipPackage
        } else {
            & $Env:EXPERT_BUILD_DIRECTORY\Build\Invoke-Build.ps1 -Task "$task" -File $Env:EXPERT_BUILD_DIRECTORY\Build\BuildProcess.ps1 -Repository $repositoryPath -Clean:$clean.ToBool() -Flavor:$flavor -CodeCoverage $codeCoverage -Integration:$integration.ToBool() -Automation:$automation.ToBool()
        }
    }

    end {
        if ($LASTEXITCODE -eq 0 -and $codeCoverageReport.IsPresent -and $codeCoverage) {
            [string]$codeCoverageReport = Join-Path -Path $repositoryPath -ChildPath "Bin\Test\CodeCoverage\dotCoverReport.html"

            if (Test-Path ($codeCoverageReport)) {
                Write-Host "Displaying dotCover code coverage report."
                Start-Process $codeCoverageReport
            } else {
                Write-Warning "Unable to locate dotCover code coverage report."
            }
        }
    }
}

function InstallPoshGit() {
    # We need Windows 10 or WMF 5 for Install-Module
    if ($host.Version.Major -ge 5) {
        try {
            if (Test-Path $Env:USERPROFILE\Documents\WindowsPowerShell\Modules\posh-git) {
                Import-Module posh-git -Global
                return
            }

            # Optimization, Get-InstalledModule is quite slow so just peek directly
            if (Test-Path $Env:ProgramFiles\WindowsPowerShell\Modules\posh-git) {
                Import-Module posh-git -Global
                return
            }
    
            if (-not (Get-InstalledModule posh-git -ErrorAction SilentlyContinue)) {
                Install-Module posh-git -Scope CurrentUser
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
            Write-Host "It will be changed to false." -ForegroundColor Red
            Write-Host (New-Object string -ArgumentList '*', 80) -ForegroundColor Red
            sleep -Seconds 10
        }
    } finally {
        # Probably don't have git so we are going to fail hard very soon.
    }

    & git config --global difftool.prompt false
    & git config --global credential.tfs.integrated true
    & git config --global credential.tfs.ap.aderant.com.integrated true
    & git config --global core.autocrlf false
    & git config --global http.emptyAuth true
    & git config --global credential.authority ntlm
    & git config --global core.excludesfile "$buildScriptsDirectory\..\..\.gitignore"
    & git config --global fetch.prune true

    # Global Aliases - Insert nifty git commands here

    # Prints a list of branches you've commited to sorted by date
    & git config --global alias.branchdates "for-each-ref --sort=committerdate refs/heads/ --format='%(committerdate:short) %(refname:short)'"

    # Deletes all untracked files without wiping out any SharedBin symlinks
    & git config --global alias.scrub "clean -fdx -e SharedBin -e .vscode/"

    # Undoes the last commit
    & git config --global alias.undo-commit "reset --soft HEAD^"

    # Delete all local branches but master and the current one, only if they are fully merged with master.
    & git config --global alias.br-delete-useless "!f(){ git branch | grep -v 'master' | grep -v ^* | xargs git branch -d; }; f"

    # set up notepad++ as the default commit editor
    # & git config --global core.editor "'C:/Program Files (x86)/Notepad++/notepad++.exe' -multiInst -notabbar -nosession -noPlugin"
}

function CheckModuleVersion() {
    # Check for PackageManagement 1.0.0.0
    Import-Module PackageManagement
    $packageManagerVerion = (Get-Module PackageManagement).Version
    if (!$packageManagerVerion) {
        Write-Warning "PackageManagement not detected, please install PackageManagement ver. 1.0.0.1 or later"
        return $false 
    }
    if ($packageManagerVerion.ToString().Equals("1.0.0.0")) {
        Write-Warning "PackageManagement Version 1.0.0.0 detected - this version is buggy and may prevent the installation of tools which enhance the developer experience. If you have issues installing tools such as posh-git using Install-Module you can try replacing the version of PackageManagement in C:\Program Files (x86)\WindowsPowerShell\Modules with a newer version from another machine"
        return $false 
    }
    return $true
}

if (CheckModuleVersion) { 
    InstallPoshGit
    Export-ModuleMember -Function Invoke-Build
    Set-Alias -Name bm -Value Invoke-Build -Scope Global
}

function global:New-PullRequest {
    [string]$currentBranch = git rev-parse --abbrev-ref HEAD
    [string]$repository = git ls-remote --get-url

    if ((git ls-remote --heads $repository $currentBranch) -ne $null) {
        [string]$url = "http://tfs:8080/tfs/ADERANT/ExpertSuite/_git/$($global:CurrentModuleName)/pullrequestcreate?sourceRef=$($currentBranch)&targetRef=master"
        Start-Process $url
    } else {
        Write-Error "No remote branch present. Use git push -u origin $($currentBranch)"
    }
}

Export-ModuleMember -Function New-PullRequest
Set-Alias -Name npr -Value New-PullRequest -Scope Global

ConfigureGit