<#
.Synopsis
    Extracts 7zip from the package
#>
[CmdletBinding()]
param(
    [string]$BuildToolsDirectory,
    [string]$RepositoryRoot
)

if ([string]::IsNullOrEmpty($RepositoryRoot)) {
    return
}

$7ipRoot = [System.IO.Path]::Combine($RepositoryRoot, "paket-files", "www.7-zip.org")

# Here is some excitement. We need to download 7zr (old) which understands the 7z format so we can download and extract the latest 7Zip tool which is
# only available in a 7z archive. Well played Igor.

# e = Extract
# -o = Where to put stuff
# -aoa = Overwrite All existing files without prompt.
Start-Process -FilePath "$7ipRoot\7zr.exe" -ArgumentList @("e", "$7ipRoot\7z1900-extra.7z", "-aoa", "-o$BuildToolsDirectory") -NoNewWindow -PassThru -Wait -WorkingDirectory $RepositoryRoot | Out-Null