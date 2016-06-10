param(
    [switch]$version,
    [string]$action = "build",    
    [string]$updateSource = "http://tfs:8080/tfs/aderant/expertsuite/_apis/git/repositories/build.infrastructure/items?scopePath=build.ps1"    
)
   
process {

    function Version {
        return [Guid]::NewGuid().ToString()
    }

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

    function RefreshThisScript() {
        Write-Host "Updating from $updateSource"
        
        if ($updateSource -like "http*") {
            $response = wget $updateSource -UseDefaultCredentials
            return $response.RawContentStream
        }     
        
        $contents = Get-Content -Raw $updateSource                
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($contents)
        return new-object System.IO.MemoryStream (,$bytes)
    }

    function Update() {
        Write-Host "Updating build system"    
        
        $stream = RefreshThisScript
        
        $sr = new-object System.IO.StreamReader ($stream)    
        $script = $sr.ReadToEnd()
        
        [string]$thisVersion = Version
        [string]$newVersion = [scriptblock]::Create($script).Invoke($true)        

        if ($thisVersion -ne $newVersion) {
            Write-Host ("Updating $PSCommandPath [Current Version: {0} New Version: {1}]" -f $thisVersion, $newVersion)
            $script | Out-File $PSCommandPath -Encoding UTF8            
        } else {
            Write-Host "No update required."
        }
    }

    function Build([string]$action){    
        $updateRequested = $action -eq "update"

        if ($PSCommandPath -and (-not $Env:EXPERT_BUILD_UTIL_DIRECTORY -or $updateRequested)) {
            Update
            
            if (-not $Env:EXPERT_BUILD_UTIL_DIRECTORY) {
                $Env:EXPERT_BUILD_UTIL_DIRECTORY = [System.IO.Path]::Combine($PSScriptRoot, ".buildutils")
                
                $zip = DownloadBuildSystem
                ExtractZip $zip $Env:EXPERT_BUILD_UTIL_DIRECTORY
            }        
            
            if (-not $updateRequested) {
                & $PSCommandPath -action build
                return
            }
        } else {
            & $Env:EXPERT_BUILD_UTIL_DIRECTORY\Build\BuildModule.ps1 $args
            Write-Host "Doing the build: $PSScriptRoot"
        }    
    }
    

    if ($version) {
        Version
        return
    }
    
    if ($action -eq "update") {
        Update
        return
    }
    
    Build
}