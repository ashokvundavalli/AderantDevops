$global:DeveloperShell = New-Object PSObject -Property @{
    # The directory which contains all of the repositories
    ExpertRepositoryPath                          = $Env:EXPERT_REPOSITORY_PATH

    # The current repository. This is where all commands execute relative to
    CurrentRepositoryPath                         = ""

    BuildSystem                                   = $Env:EXPERT_BUILD_ROOT

    DropLocation                                  = "\\na.aderant.com\ExpertSuite\Main\"
}

$s = $global:DeveloperShell

# Things to extract later
$currentUser = [Security.Principal.WindowsPrincipal]([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdminProcess = $currentUser.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

$adminHeader = if ($isAdminProcess) { 'Administrator: ' } else { '' }

function GetPathFromUser([ScriptBlock]$promptAction) {
    do {
        & $promptAction

        $path = Read-Host -Prompt "Path"
    } while (-not $path -or -not (Test-Path $path))

    return [System.IO.Path]::GetFullPath($path)
}
#

function WriteRepositoryInfo {
    $Host.UI.RawUI.WindowTitle = "{0} [{1}]" -f $adminHeader, $s.CurrentRepositoryPath
        
    Write-Host ""
    Write-Host "Repository [" -NoNewline
    Write-Host $s.CurrentRepositoryPath -NoNewline -ForegroundColor DarkCyan     
    Write-Host "]" 
}

function SetBuildSystemPathFromCurrentFile() {
    $path = [System.IO.Path]::GetFullPath((Get-Module Aderant).Path + "..\..\..\..\")
    
    $s.BuildSystem = $path
    
    [Environment]::SetEnvironmentVariable("EXPERT_BUILD_ROOT", $path, "User")
}

function Enable-GitPrompt {

    function global:prompt {
        $realLASTEXITCODE = $LASTEXITCODE

        Write-Host($pwd.ProviderPath) -NoNewline

        Write-VcsStatus

        WriteRepositoryInfo                
       
        $global:LASTEXITCODE = $realLASTEXITCODE
        
        # Default console looks like this
        # PS C:\WINDOWS\system32>
        Write-Host ("PS " + $(Get-Location) + ">") -NoNewline 
        return " "
    }
    
}

function global:Set-Repository([string]$repositoryPath) {   
    if (Test-Path $repositoryPath) {
        if (gci $repositoryPath -Filter ".git" -Directory -Hidden) {
            $s.CurrentRepositoryPath = Resolve-Path $repositoryPath
        } else {
            Write-Error "The path $path does not contain a git repository"
        }
    }

    Write-Debug $global:DeveloperShell
}

function global:Get-Dependencies() {
    if ([string]::IsNullOrEmpty($s.CurrentRepositoryPath)) {
        Write-Warning "The current repository is not set so the depenencides will not be fetched"
    } else {
        $shell = ".\LoadDependencies.ps1 -modulesRootPath {0} -dropPath {1}" -f $s.CurrentRepositoryPath, $s.DropLocation
            
        $dir = "$($s.BuildSystem)\Build"
        pushd $dir
        Invoke-Expression $shell
        popd
        return    
    }
   
}

function InitializeEnvironment() {
    # Export functions and variables we want external to this script
    $functionsToExport = @( 
        @{ function="Get-Dependencies";                     alias="gd"}
        @{ function="Set-Repository";                       alias=""}
    )
    
    foreach ($toExport in $functionsToExport) {
        Write-Debug "Exporting $($toExport.function)"

        Export-ModuleMember -function $toExport.function
        if ($toExport.alias) {
            Set-Alias $toExport.alias $toExport.function -Scope Global
            Export-ModuleMember -alias $toExport.alias
        }
    }
    
    if (-not ($s.BuildSystem)) {
        SetBuildSystemPathFromCurrentFile
    }
}

function InstallPoshGit() {
    # We need Windows 10 or WMF 5 for Install-Module
    if ($host.Version.Major -ge 5) {
    
        if (-not (Get-InstalledModule posh-git)) {
            Install-Module posh-git
        }
    }
}


function global:Enable-GitIntegration() {
    if (-not $s.ExpertRepositoryPath)  {
        Write-Host "=== Welcome to Git ==="      

        Write-Host ""
        Write-Host "You will be asked a series of questions which you are expected to answer truthfully."
        Write-Host

        $promptAction = {                       
            Write-Host "Where is your Git repository home? This is the directory where all of your Git repostories will live."
            Write-Host "For example C:\Source\"
        }

        $path = GetPathFromUser $promptAction
                
        [Environment]::SetEnvironmentVariable("EXPERT_REPOSITORY_PATH", $path, "User")
        
        $s.ExpertRepositoryPath = $path
    }

    InstallPoshGit    
    
    if (Get-InstalledModule posh-git) {
        Import-Module posh-git -Global    
        Enable-GitPrompt
    }

    Set-Location $s.ExpertRepositoryPath

    InitializeEnvironment
}

Export-ModuleMember -Function Enable-GitIntegration