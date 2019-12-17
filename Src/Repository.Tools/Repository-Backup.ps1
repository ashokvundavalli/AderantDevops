<#
.Synopsis
    Clones the specified Git repositories from TFS for offsite backup.
.Description
    Clones the specified Git repositories from TFS for offsite backup.
.PARAMETER stagingDirectory
    The directory to clone the repositories to.
.PARAMETER repositories
    The names of the repositores to clone
.EXAMPLE
    & '.\Repository-Backup.ps1' - stagingDirectory 'C:\Temp\Backup' -repositores 'Build.Infrastructure', 'Deployment'
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$stagingDirectory,
    [Parameter(Mandatory=$true)][ValidateNotNull()][string[]]$repositories,
    [Parameter(Mandatory=$true)][ValidateNotNull()][string]$tag,
    [Parameter(Mandatory=$false)][ValidateNotNull()][int16]$timeout = 120
)

Set-StrictMode -version 'Latest';
$ErrorActionPreference = 'Stop';
$InformationPreference = 'Continue';

# Clear staging directory if it exists.
If ([System.IO.Directory]::Exists($stagingDirectory)) {
    Remove-Item -Path $stagingDirectory -Recurse -Force;
}

# Create staging directory.
New-Item -ItemType Directory -Path $stagingDirectory -Force;

[string]$7zip = "$Env:ProgramFiles\7-Zip\7z.exe";

if (-not [System.IO.File]::Exists($7zip)) {
    Write-Error "7-Zip is not installed at path: '$7zip'.";
    return;
}

[System.Collections.ArrayList]$jobs = @();

foreach ($repository in $repositories) {
    [void]$jobs.Add(
        (Start-Job -Name "SourceControlBackup_$repository" -ScriptBlock {
param (
    [Parameter(Mandatory=$true)][string]$stagingDirectory,
    [Parameter(Mandatory=$true)][string]$repository,
    [Parameter(Mandatory=$true)][string]$tag,
    [Parameter(Mandatory=$true)][string]$7zip
)

[string]$outputDirectory = [System.IO.Path]::Combine($stagingDirectory, $repository);
git.exe clone https://tfs.aderant.com/tfs/ADERANT/ExpertSuite/_git/$repository --branch $tag --depth 1 --shallow-submodules $outputDirectory;

if ([System.IO.Directory]::Exists($outputDirectory)) {
    & $7zip a -tzip "$stagingDirectory\$repository.zip" "$outputDirectory\*" -mx9 -mm=LZMA;
}
        } -ArgumentList $stagingDirectory, $repository, $tag, $7zip)
    );
}

Wait-Job -Id $jobs.Id -Timeout $timeout -;

Remove-Job -id $jobs.Id;