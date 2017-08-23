$currentUser = [Security.Principal.WindowsPrincipal]([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdminProcess = $currentUser.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$adminHeader = if ($isAdminProcess) { 'Administrator: ' } else { '' }

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

function global:Invoke-Build([switch]$force, [switch]$clean, [switch]$package, [switch]$debug, [switch]$release) {
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
	
    if ($package) {
        $task = "Package"
    }    

    & $Env:EXPERT_BUILD_DIRECTORY\Build\Invoke-Build.ps1 -Task "$task" -File $Env:EXPERT_BUILD_DIRECTORY\Build\BuildProcess.ps1 -Repository $repositoryPath -Clean:$clean.ToBool() -Flavor:$flavor
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


    # set up notepad++ as the default commit editor
    # & git config --global core.editor "'C:/Program Files (x86)/Notepad++/notepad++.exe' -multiInst -notabbar -nosession -noPlugin"
}

function CheckModuleVersion(){
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

Export-ModuleMember -Function New-PullRequest -Alias cpr

ConfigureGit	
