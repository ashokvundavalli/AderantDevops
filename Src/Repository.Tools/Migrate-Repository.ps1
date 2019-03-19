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
git pull
git add . --force
git reset --hard
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
git pull
git add .
git reset --hard
git branch -D "relocate/$sourceBranch/$module"
git checkout -b "relocate/$sourceBranch/$module"
git remote add $module $sourceRepository
git fetch $module
git merge $module/$sourceTempBranch --allow-unrelated-histories
git remote remove $module
git push --set-upstream origin "relocate/$sourceBranch/$module"

if ($createPullRequest.IsPresent) {
    [string]$targetModule = [System.IO.Path]::GetFileName($targetRepository)
    New-PullRequest -sourceModule $targetModule -targetBranch $destinationBranch
}