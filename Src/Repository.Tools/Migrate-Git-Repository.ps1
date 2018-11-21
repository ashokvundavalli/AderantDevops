param (
    [Parameter(Mandatory=$true)][string]$sourceRepository,
    [Parameter(Mandatory=$true)][string]$targetRepository
)

Set-StrictMode -Version 'Latest'
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -Path $sourceRepository)) {
    Write-Error "$sourceRepository does not exist."
    exit 1
}

if (-not (Test-Path -Path $targetRepository)) {
    Write-Error "$targetRepository does not exist."
    exit 1
}

[string]$module = [System.IO.Path]::GetFileName($sourceRepository)

Push-Location -Path $targetRepository

git checkout master
git pull
git checkout -b TFVC/$module
git remote add $module $sourceRepository
git fetch $module
git merge $module/master --allow-unrelated-histories
git remote remove $module
git push -u

Pop-Location