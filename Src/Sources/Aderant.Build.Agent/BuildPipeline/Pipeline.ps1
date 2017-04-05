Set-StrictMode -Version 2

[CmdletBinding()]

$ErrorActionPreference = 'Stop'

[string]$repository = "default"
[string]$version = "master"
[string]$customSource = $null

$function = Get-ChildItem Function:\Get-VstsInput -ErrorAction SilentlyContinue
if (-not ($function)) {
    # If the Vsts module isn't available then we will just cook up a stub function

    function Get-VstsInput([string]$name) {
        if ($name -eq "Repository") {
            return $repository
        }

        if ($name -eq "Branch") {
            return $version
        }
        return $null
    }
}

$repository = Get-VstsInput -Name 'Repository'
$version = Get-VstsInput -Name 'Branch'
$customSource = Get-VstsInput -Name 'CustomSource'

Write-Host "Repository: $repository"
Write-Host "Version: $version"
Write-Host "CustomSource: $customSource"

Write-Host "SYSTEM_TEAMPROJECT: $ENV:SYSTEM_TEAMPROJECT"
Write-Host "SYSTEM_TEAMFOUNDATIONSERVERURI: $ENV:SYSTEM_TEAMFOUNDATIONSERVERURI"
Write-Host "SYSTEM_TEAMFOUNDATIONCOLLECTIONURI: $ENV:SYSTEM_TEAMFOUNDATIONCOLLECTIONURI"
Write-Host "SYSTEM_COLLECTIONID: $ENV:SYSTEM_COLLECTIONID"
Write-Host "SYSTEM_DEFAULTWORKINGDIRECTORY: $ENV:SYSTEM_DEFAULTWORKINGDIRECTORY"
Write-Host "BUILD_DEFINITIONNAME: $ENV:BUILD_DEFINITIONNAME"
Write-Host "BUILD_DEFINITIONVERSION: $ENV:BUILD_DEFINITIONVERSION"
Write-Host "BUILD_BUILDNUMBER: $ENV:BUILD_BUILDNUMBER"
Write-Host "BUILD_BUILDURI: $ENV:BUILD_BUILDURI"
Write-Host "BUILD_BUILDID: $ENV:BUILD_BUILDID"
Write-Host "BUILD_QUEUEDBY: $ENV:BUILD_QUEUEDBY"
Write-Host "BUILD_QUEUEDBYID: $ENV:BUILD_QUEUEDBYID"
Write-Host "BUILD_REQUESTEDFOR: $ENV:BUILD_REQUESTEDFOR"
Write-Host "BUILD_REQUESTEDFORID: $ENV:BUILD_REQUESTEDFORID"
Write-Host "BUILD_SOURCEVERSION: $ENV:BUILD_SOURCEVERSION"
Write-Host "BUILD_SOURCEBRANCH: $ENV:BUILD_SOURCEBRANCH"
Write-Host "BUILD_SOURCEBRANCHNAME: $ENV:BUILD_SOURCEBRANCHNAME"
Write-Host "BUILD_REPOSITORY_NAME: $ENV:BUILD_REPOSITORY_NAME"
Write-Host "BUILD_REPOSITORY_PROVIDER: $ENV:BUILD_REPOSITORY_PROVIDER"
Write-Host "BUILD_REPOSITORY_CLEAN: $ENV:BUILD_REPOSITORY_CLEAN"
Write-Host "BUILD_REPOSITORY_URI: $ENV:BUILD_REPOSITORY_URI"
Write-Host "BUILD_REPOSITORY_TFVC_WORKSPACE: $ENV:BUILD_REPOSITORY_TFVC_WORKSPACE"
Write-Host "BUILD_REPOSITORY_TFVC_SHELVESET: $ENV:BUILD_REPOSITORY_TFVC_SHELVESET"
Write-Host "BUILD_REPOSITORY_GIT_SUBMODULECHECKOUT: $ENV:BUILD_REPOSITORY_GIT_SUBMODULECHECKOUT"
Write-Host "AGENT_NAME: $ENV:AGENT_NAME"
Write-Host "AGENT_ID: $ENV:AGENT_ID"
Write-Host "AGENT_HOMEDIRECTORY: $ENV:AGENT_HOMEDIRECTORY"
Write-Host "AGENT_ROOTDIRECTORY: $ENV:AGENT_ROOTDIRECTORY"
Write-Host "AGENT_WorkFolder: $ENV:AGENT_WorkFolder"
Write-Host "AGENT_BUILDDIRECTORY: $ENV:AGENT_BUILDDIRECTORY"
Write-Host "BUILD_REPOSITORY_LOCALPATH: $ENV:BUILD_REPOSITORY_LOCALPATH"
Write-Host "BUILD_SOURCESDIRECTORY: $ENV:BUILD_SOURCESDIRECTORY"
Write-Host "BUILD_ARTIFACTSTAGINGDIRECTORY: $ENV:BUILD_ARTIFACTSTAGINGDIRECTORY"
Write-Host "BUILD_STAGINGDIRECTORY: $ENV:BUILD_STAGINGDIRECTORY"

Get-Variable |%{ Write-Host ("Name : {0}, Value: {1}" -f $_.Name,$_.Value ) }

$pathToGit = "C:\Program Files\Git\cmd\git.exe"

function SetGitOptions() {  
    if (-not (Test-Path $pathToGit)) {
        throw "Git.exe not found at $pathToGit"
    }

    $hosts = @("tfs", "tfs.ap.aderant.com")
    foreach ($entry in $hosts) { # Host is a reserved readonly PowerShell variable and so cannot be assigned to
        & $pathToGit config --global credential.$entry.interactive never
        & $pathToGit config --global credential.$entry.integrated true                
    }
    & $pathToGit config --global http.emptyAuth true
    & $pathToGit config --global credential.authority ntlm
}

function CloneRepo([string]$repo, [string]$version) {
    $directory = GetCloneTargetDirectory ($repo + $version)
  
    [bool]$doClone = $true

    $directories = @("D:\", [System.IO.Path]::GetTempPath())

    foreach ($dir in $directories) {
        if (Test-Path $dir) {
            $target = [System.IO.Path]::Combine($dir, "Temp", $directory) # this may create C:\Users\<name>\AppData\Local\Temp\Temp\... but we don't care 
            
            if (Test-Path $target) {
                $doClone = $false
            }

            Write-Debug "Target: $target"

            $directory = $target
        }
    }

    if ($doClone) {    
        Write-Host "About to clone $repo ($version)"

        & "$pathToGit" "clone" "$repo" "--branch" "$version" "--single-branch" "$directory" | Out-Host
    } else {
        Push-Location $directory

        Write-Host "About to update $repo in $directory ($version)"
        
        & "$pathToGit" "reset" "--hard" "HEAD" | Out-Host
        & "$pathToGit" "fetch" "--all" | Out-Host
        & "$pathToGit" "reset" "--hard" "origin/$version" | Out-Host
        
        Pop-Location
    }

    return $directory
}

function GetCloneTargetDirectory([string]$text) {
    $hashAlgorithm = [System.Security.Cryptography.HashAlgorithm]::Create("SHA1")
    $hash = $hashAlgorithm.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($text))
        
    return [System.BitConverter]::ToString($hash).Replace("-", "")
}

function SetEnvironmentVariables([string]$directory) {    
    $expertBuildDirectory = [System.IO.Path]::Combine($directory, "Src")    

    if (-not (Test-Path $expertBuildDirectory)) {
        throw "Fatal: build directory $expertBuildDirectory does not exist"
    }

    [System.Environment]::SetEnvironmentVariable("EXPERT_BUILD_DIRECTORY", $expertBuildDirectory, [System.EnvironmentVariableTarget]::Process)

    Write-Host ("##vso[task.setvariable variable=EXPERT_BUILD_DIRECTORY;]$expertBuildDirectory")
}

function CloneBuildSystem() {
    if ($repository -eq "default" -or -not $customSource) {
        # By default use script from checked in code at Build.Infrastructure at TFS
        return CloneRepo "http://tfs:8080/tfs/ADERANT/ExpertSuite/_git/Build.Infrastructure" $version
    } else {
        # During debug of this script it is necessary to use a local copy 
        if ($customSource -and -not [string]::IsNullOrEmpty($customSource)) {
            if ($customSource.StartsWith("http")) {        
                return CloneRepo $customSource $version
            } else {
                $buildFolder = [System.IO.Path]::Combine($Env:BUILD_SOURCESDIRECTORY, "_BUILD_" + (Get-Random))    
                # e.g \\wsakl001092\c$\Source\Build.Infrastructure
                Write-Host "Copying from path $customSource"        
                Copy-Item $customSource $buildFolder -Recurse

                return $buildFolder
            }   
        }
    }
}

# Force some sensible options so we don't get prompted for credentials
SetGitOptions

$buildFolder = CloneBuildSystem

SetEnvironmentVariables $buildFolder

Set-StrictMode -Off

& $Env:EXPERT_BUILD_DIRECTORY\Build\Invoke-Build.ps1 -File $Env:EXPERT_BUILD_DIRECTORY\Build\BuildProcess.ps1 -Repository $Env:BUILD_SOURCESDIRECTORY