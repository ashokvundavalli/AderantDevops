function DownloadBuildSystem() {
    $zip = "$Env:Temp\$([System.IO.Path]::GetRandomFileName()).zip"

    Write-Host "Downloading build system to" $zip

    $wc = New-Object System.Net.WebClient
    $wc.UseDefaultCredentials = $true
    $wc.Headers.Add("accept", "application/zip")
    $wc.DownloadFile("http://tfs:8080/tfs/aderant/expertsuite/_apis/git/repositories/build.infrastructure/items?scopePath=Src&versionType=branch&version=master", $zip)

    Write-Host "Complete."

    return $zip
}

function ExtractZip($zipPath, $destinationPath) {
    Add-Type -AssemblyName "System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
    Add-Type -AssemblyName "System.IO.Compression.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"

    New-Item -ItemType Directory -Path $destinationPath -ErrorAction SilentlyContinue | Out-Null
    
    Write-Host "Extracting zip to " $destinationPath

    # Bug in the TFS zip stream - the zip archive contains an empty entry with the same name as the requested scopePath which we need to ignore so we have to extract the long way
    $archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Read)
    foreach ($entry in $archive.Entries) {
        if ($entry.Length -gt 0) {
            
            $entryFullName = $entry.FullName
            $entryFullName = $entryFullName.TrimStart("Src\")
        
            $destinationFile = "$destinationPath\$entryFullName"
            New-Item -ItemType File -Path $destinationFile -Force | Out-Null
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destinationFile, $true)
        }
    }

    Write-Host "Extracted build system to $destinationPath" 
    $archive.Dispose()
    Remove-Item $zip -Force
}

function Build() {  
    [CmdletBinding()]
    param(
        [string[]] $Task = @('Default'),

        [ValidateSet('Release', 'Debug')]
        [string] $Configuration = 'Debug'
    )

    if ($Env:SYSTEM_DEFINITIONID) {
        $Env:EXPERT_BUILD_FOLDER = [System.IO.Path]::Combine($PSScriptRoot, "_BUILD_")
    
        $zip = DownloadBuildSystem
        ExtractZip $zip $Env:EXPERT_BUILD_FOLDER
    }
    
    if (-not $Env:EXPERT_BUILD_FOLDER) {
        throw "There is no build root environment variable defined. Build cannot continue."
        return
    }
    
    if (-not (Test-Path $Env:EXPERT_BUILD_FOLDER)) {
        throw "EXPERT_BUILD_FOLDER is defined but the target does not exist. Build cannot continue."
    }
        
    & $Env:EXPERT_BUILD_FOLDER\Invoke-Build.ps1 -File $Env:EXPERT_BUILD_FOLDER\BuildProcess.ps1 -Task $Task -Configuration $Configuration -Repository $PSScriptRoot
}    

Build $args