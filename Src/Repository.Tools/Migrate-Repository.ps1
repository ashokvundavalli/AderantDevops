[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$sourceRepository,
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$sourceBranch,
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$targetRepository,
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$destinationBranch,
    [switch]$prepareSource,
    [Alias('pr')][switch]$createPullRequest
)

Set-StrictMode -Version 'Latest'
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -Path $sourceRepository)) {
    Write-Error "Origin: '$sourceRepository' does not exist."
}

if (-not (Test-Path -Path $targetRepository)) {
    Write-Error "Origin: '$targetRepository' does not exist."
}

# Prepare origin
Push-Location -Path $sourceRepository
git checkout $sourceBranch --force
git add . --force
git reset --hard
git pull
git clean -fdx

[string]$module = [System.IO.Path]::GetFileName($sourceRepository)

[string]$sourceTempBranch = "relocate/$module"
git branch -D $sourceTempBranch
git checkout -b $sourceTempBranch

if ($prepareSource.IsPresent) {
    [string[]]$files = (Get-ChildItem -Path $sourceRepository).FullName
    [string]$moduleDir = Join-Path -Path $sourceRepository -ChildPath $module

    New-Item -ItemType Directory -Path $moduleDir -Force

    foreach ($file in $files) {
        Move-Item -Path $file -Destination $file.Replace($sourceRepository, $moduleDir) -Force
    }

    git add . --force
    git commit -m "Moved $module to directory"
}

Pop-Location

# Migrate to target
Push-Location -Path $targetRepository

git checkout $destinationBranch --force
git add .
git reset --hard
git pull
git branch -D "relocate/$module"
git checkout -b "relocate/$module"
git remote add $module $sourceRepository
git fetch $module
git merge $module/$sourceTempBranch --allow-unrelated-histories
git remote remove $module
git push -u

if ($createPullRequest.IsPresent) {
    New-PullRequest -targetBranch $destinationBranch
}