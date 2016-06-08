[string]$path = $global:MyInvocation.MyCommand.Path

if (-not $path) {
	$path = Get-Location
} else {
	$path = Split-Path -Parent $path
}

$repositoryDirectory = $path

function DownloadBuildSystem() {
    $zip = "$Env:Temp\$([System.IO.Path]::GetRandomFileName()).zip"

    Write-Host "Downloading build system to" $zip

    $wc = New-Object System.Net.WebClient
    $wc.UseDefaultCredentials = $true
    $wc.Headers.Add("accept", "application/zip")
    $wc.DownloadFile("http://tfs:8080/tfs/aderant/expertsuite/_apis/git/repositories/build.infrastructure/items?scopePath=Src", $zip)

    Write-Host "Complete."

    return $zip
}

function ExtractZip($zipPath, $destinationPath) {
    Add-Type -AssemblyName "System.IO.Compression.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"

    New-Item -ItemType Directory -Path $destinationPath -ErrorAction SilentlyContinue | Out-Null

    # Bug in the TFS zip stream - the zip archive contains an empty entry with the same name as the requested scopePath which we need to ignore so we have to extract the long way
    $archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Read)
    foreach ($entry in $archive.Entries) {
        if ($entry.Length -gt 0) {
            $destinationFile = "$destinationPath\$($entry.FullName)"
            New-Item -ItemType File -Path $destinationFile -Force | Out-Null
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destinationFile, $true)
        }
    }

    $archive.Dispose()
    Remove-Item $zip -Force
}

$zip = DownloadBuildSystem
ExtractZip $zip "$repositoryDirectory\.Build"