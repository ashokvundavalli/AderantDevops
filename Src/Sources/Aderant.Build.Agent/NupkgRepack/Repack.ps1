# NuGet encodes spaces as %20, we don't want this so we UrlDecode the entries in the package after packaging
[CmdletBinding()]
param (    
    [string]$searchPattern = "**\*.nupkg"
)

Import-Module $PSScriptRoot\ps_modules\VstsTaskSdk

Add-Type -AssemblyName "System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
Add-Type -AssemblyName "System.IO.Compression.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
Add-Type -AssemblyName "System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"

function GetVstsInputField([string]$path){
    $value = Get-VstsInput -Name "$path"
    Write-Host "$($path): $value"
    return $value
}

$searchPattern = GetVstsInputField "searchPattern"

Write-Verbose "Checking pattern is specified"
if(!$searchPattern)
{
    throw "Search pattern parameter must be set"
}

# check for solution pattern
if ($searchPattern.Contains("*") -or $searchPattern.Contains("?") -or $searchPattern.Contains(";"))
{
    Write-Verbose "Pattern found in solution parameter."    
    if ($env:BUILD_SOURCESDIRECTORY)
    {
        Write-Verbose "Using build.sourcesdirectory as root folder"
        Write-Host "Find-VstsFiles -LegacyPattern $searchPattern -LiteralDirectory $env:BUILD_SOURCESDIRECTORY"
        $foundFiles = Find-VstsFiles -LegacyPattern $searchPattern -LiteralDirectory $env:BUILD_SOURCESDIRECTORY
    }
    elseif ($env:SYSTEM_ARTIFACTSDIRECTORY)
    {
        Write-Verbose "Using system.artifactsdirectory as root folder"
        Write-Host "Find-VstsFiles -LegacyPattern $searchPattern -LiteralDirectory $env:SYSTEM_ARTIFACTSDIRECTORY"
        $foundFiles = Find-VstsFiles -LegacyPattern $searchPattern -LiteralDirectory $env:SYSTEM_ARTIFACTSDIRECTORY
    }
    else
    {
        Write-Host "Find-VstsFiles -LegacyPattern $searchPattern"
        $foundFiles = Find-VstsFiles -LegacyPattern $searchPattern
    }
}
else
{
    Write-Verbose "No pattern found in solution parameter."
    $foundFiles = ,$searchPattern
}

$foundCount = $foundFiles.Count
Write-Verbose "Found files: $foundCount"
foreach ($fileToPackage in $foundFiles)
{
    Write-Verbose "--File: `"$fileToPackage`""
}

$packages = $foundFiles

function RemoveEncoding([System.IO.Compression.ZipArchive]$archive, [System.IO.Compression.ZipArchiveEntry]$entry) {
    $decodedName = [System.Net.WebUtility]::UrlDecode($entry.FullName)

    if ($name -ne $entry.FullName) {
        Write-Host "Renaming entry $($entry.FullName)"

        $newEntry = $archive.CreateEntry($decodedName)

        $source = $entry.Open()
        $target = $newEntry.Open()

        $source.CopyTo($target)


        $source.Dispose()
        $target.Dispose()

        $entry.Delete()
    }    
}

foreach ($package in $packages) {
    Write-Host "Processing archive $package"

    $archive = [System.IO.Compression.ZipArchive]::new([System.IO.File]::Open($package, [System.IO.FileMode]::Open), [System.IO.Compression.ZipArchiveMode]::Update)

    foreach ($entry in [System.Linq.Enumerable]::ToList($archive.Entries)) {
        RemoveEncoding $archive $entry
    }

    $archive.Dispose()
}