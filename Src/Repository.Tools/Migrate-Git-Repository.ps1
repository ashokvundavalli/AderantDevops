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

[string[]]$files = (Get-ChildItem -Path $sourceRepository).FullName
[string]$moduleDir = Join-Path -Path $sourceRepository -ChildPath $module

New-Item -ItemType Directory -Path $moduleDir

foreach ($file in $files) {
    Move-Item -Path $file -Destination $file.Replace($sourceRepository, $moduleDir) -Force
}

Push-Location -Path $sourceRepository

git add . -f
git commit -m "Moved $module to directory"

Pop-Location

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