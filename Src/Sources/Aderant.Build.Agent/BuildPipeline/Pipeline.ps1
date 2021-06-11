Set-StrictMode -Version 'Latest'
[CmdletBinding()]

$ErrorActionPreference = 'Stop'

[string]$repository = Get-VstsInput -Name 'Repository'
[string]$version = Get-VstsInput -Name 'Branch'
[string]$CustomSource = Get-VstsInput -Name 'CustomSource'

if ([string]::IsNullOrWhiteSpace($version)) {
    $version = 'master'
}

Write-Host "Repository: $repository"
Write-Host "Version: $version"
Write-Host "CustomSource: $CustomSource"

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
Write-Host "BUILD_REASON: $ENV:BUILD_REASON"
Write-Host "AGENT_ID: $ENV:AGENT_ID"
Write-Host "AGENT_NAME: $ENV:AGENT_NAME"
Write-Host "AGENT_MACHINENAME: $ENV:AGENT_MACHINENAME"
Write-Host "AGENT_HOMEDIRECTORY: $ENV:AGENT_HOMEDIRECTORY"
Write-Host "AGENT_ROOTDIRECTORY: $ENV:AGENT_ROOTDIRECTORY"
Write-Host "AGENT_WORKFOLDER: $ENV:AGENT_WORKFOLDER"
Write-Host "AGENT_BUILDDIRECTORY: $ENV:AGENT_BUILDDIRECTORY"
Write-Host "BUILD_REPOSITORY_LOCALPATH: $ENV:BUILD_REPOSITORY_LOCALPATH"
Write-Host "BUILD_SOURCESDIRECTORY: $ENV:BUILD_SOURCESDIRECTORY"
Write-Host "BUILD_ARTIFACTSTAGINGDIRECTORY: $ENV:BUILD_ARTIFACTSTAGINGDIRECTORY"
Write-Host "BUILD_STAGINGDIRECTORY: $ENV:BUILD_STAGINGDIRECTORY"
Write-Host "SYSTEM_PULLREQUEST_TARGETBRANCH: $ENV:SYSTEM_PULLREQUEST_TARGETBRANCH"
Write-Host "SYSTEM_PULLREQUEST_SOURCEBRANCH: $ENV:SYSTEM_PULLREQUEST_SOURCEBRANCH"
Write-Host "SYSTEM_PULLREQUEST_PULLREQUESTID: $ENV:SYSTEM_PULLREQUEST_PULLREQUESTID"
Get-Variable | ForEach-Object { Write-Host ("Name : {0}, Value: {1}" -f $_.Name,$_.Value ) }

$pathToGit = "C:\Program Files\Git\cmd\git.exe"

# Clean up other cloned repositories in case we are recycling a working dir
Get-ChildItem -Path $Env:BUILD_SOURCESDIRECTORY -Depth 1 -Filter "*_BUILD_*" -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

$buildFolder = [System.IO.Path]::Combine($Env:BUILD_SOURCESDIRECTORY, "_BUILD_" + $ENV:BUILD_BUILDID)

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

function CloneRepo {
    param (
        [Parameter(Mandatory=$true)][string]$repo,
        [Parameter(Mandatory=$false)][string]$version = 'master',
        [switch]$shallow
    )

    Write-Host "About to clone $repo ($version)"

    [string]$options = '--single-branch'

    if ($shallow.IsPresent) {
        $options = '--depth 1 --shallow-submodules'
    }

    cmd.exe /c ""`"$pathToGit`"" clone $repo --branch $version $options $buildFolder 2>&1"
}

if ($repository -eq "default" -or -not $CustomSource) {
    # By default use script from checked in code at Build.Infrastructure at TFS
    CloneRepo -repo 'https://tfs.aderant.com/tfs/ADERANT/ExpertSuite/_git/Build.Infrastructure' -version $version -shallow
} else {
    # During debug of this script it is necessary to use a local copy
    if ($CustomSource -and -not [string]::IsNullOrEmpty($CustomSource)) {
        if ($CustomSource.StartsWith("http")) {
            CloneRepo -repo $CustomSource -version $version
        } else {
            # e.g \\machine\c$\Source\Build.Infrastructure
            Write-Host "Copying from path $CustomSource"
            Copy-Item $CustomSource $buildFolder -Recurse
        }
    }
}

# Force some sensible options so we don't get prompted for credentials
SetGitOptions

$buildInfrastructurePath = [System.IO.Path]::Combine($buildFolder, "Src")

[System.Environment]::SetEnvironmentVariable("EXPERT_BUILD_DIRECTORY", $buildInfrastructurePath, [System.EnvironmentVariableTarget]::Process)

Write-Host ("##vso[task.setvariable variable=EXPERT_BUILD_DIRECTORY;]$buildInfrastructurePath")

Set-StrictMode -Off

& $Env:EXPERT_BUILD_DIRECTORY\Build\BuildProcess.ps1 -Repository $Env:BUILD_SOURCESDIRECTORY