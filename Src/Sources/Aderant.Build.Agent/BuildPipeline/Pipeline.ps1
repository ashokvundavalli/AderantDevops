﻿[cmdletbinding()]

$ErrorActionPreference = 'Stop'

[string]$repository = Get-VstsInput -Name 'Repository'
[string]$version = Get-VstsInput -Name 'Version'
[string]$customRepository = Get-VstsInput -Name 'CustomRepository'

Write-Host "Repository: $repository"
Write-Host "Version: $version"
Write-Host "CustomRepository: $customRepository"

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
Write-Host "BUILD_REPOSITORY_LOCALPATH: $ENV:BUILD_REPOSITORY_LOCALPATH"
Write-Host "BUILD_SOURCESDIRECTORY: $ENV:BUILD_SOURCESDIRECTORY"
Write-Host "BUILD_ARTIFACTSTAGINGDIRECTORY: $ENV:BUILD_ARTIFACTSTAGINGDIRECTORY"
Write-Host "BUILD_STAGINGDIRECTORY: $ENV:BUILD_STAGINGDIRECTORY"
Write-Host "AGENT_BUILDDIRECTORY: $ENV:AGENT_BUILDDIRECTORY"
Get-Variable |%{ Write-Host ("Name : {0}, Value: {1}" -f $_.Name,$_.Value ) }

$buildFolder = [System.IO.Path]::Combine($Env:BUILD_SOURCESDIRECTORY, "_BUILD_" + (Get-Random))    

function Clone($repo, $version) {
    if (-not $version) {
        $version = "master"
    }
    Write-Output "About to clone $repo ($version)"
    cmd /c "git clone $repo --branch $version --single-branch $buildFolder 2>&1"  
}

if ($customRepository -and -not [string]::IsNullOrEmpty($customRepository)) {
    if ($customRepository.StartsWith("http")) {        
        Clone $customRepository $version
    } else {
        # e.g \\wsakl001092\c$\Source\Build.Infrastructure\Src\
        Write-Output "Copying from path $customRepository"        
        Copy-Item $customRepository $buildFolder\Src -Recurse
    }   
}

if ($repository -eq "default" -or -not $customRepository) {
    Clone "http://tfs:8080/tfs/ADERANT/ExpertSuite/_git/Build.Infrastructure" $version
}

$buildInfrastructurePath = [System.IO.Path]::Combine($buildFolder, "Src")
    
[System.Environment]::SetEnvironmentVariable("EXPERT_BUILD_DIRECTORY", $buildInfrastructurePath, [System.EnvironmentVariableTarget]::Process)

Write-Host ("##vso[task.setvariable variable=EXPERT_BUILD_DIRECTORY;]$buildInfrastructurePath")

& $Env:EXPERT_BUILD_DIRECTORY\Build\Invoke-Build.ps1 -File $Env:EXPERT_BUILD_DIRECTORY\Build\BuildProcess.ps1 -Repository $Env:BUILD_SOURCESDIRECTORY