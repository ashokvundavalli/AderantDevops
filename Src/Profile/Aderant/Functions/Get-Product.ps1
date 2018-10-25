<#
.Synopsis
    Runs a GetProduct for the current branch
.Description
    Uses the expertmanifest from the local Build.Infrastructure\Src\Package directory.
    This will always return the pdb's.
    The binaries will be loaded into your branch binaries directory. e.g. <your_branch_source>\Binaries
.PARAMETER onlyUpdated
    Switch to indicate that only updated modules should get pulled in.
.PARAMETER createBackup
    Switch to create a backup of the Binaries folder (named BinariesBackup in the same folder) after successfully retrieving the product.
    This is intended to be used by developers who call Copy-BinariesFromCurrentModules (cb) or Copy-BinToEnvironment and want to have a backup with the original files from the Get-Product call.
.PARAMETER pullRquestId
    Mixes the output of a pull request build into the product
.EXAMPLE
    Get-Product -createBackup
#>
function Get-Product {
param (
        [switch]$createBackup,
        [switch]$onlyUpdated,
        [alias("pr")]
        [string]$pullRquestId
    )

    $buildInfrastructure = $ShellContext.PackageScriptsDirectory.Replace("Package", "")

    & tf.exe vc "get" $ShellContext.ProductManifestPath
            
    & "$($ShellContext.PackageScriptsDirectory)\GetProduct.ps1" -ProductManifestPath $ShellContext.ProductManifestPath -dropRoot $ShellContext.BranchServerDirectory -binariesDirectory $ShellContext.BranchBinariesDirectory -getDebugFiles 1 -systemMapConnectionString (Get-SystemMapConnectionString) -pullRequest $pullRquestId

    if ($createBackup) {
        Write-Host "Creating backup of Binaries folder."
        $backupPath = $ShellContext.BranchLocalDirectory + "\BinariesBackup"
        if (-not (Test-Path $backupPath)) {
            New-Item -ItemType Directory -Path $backupPath
        }
        Invoke-Expression "robocopy.exe $ShellContext.BranchBinariesDirectory $backupPath /MIR /SEC /TEE /R:2 /XD $ShellContext.BranchBinariesDirectory\ExpertSource\Customization" | Out-Null
        Write-Host "Backup complete."
    }
}

<#
.Synopsis
    Gets the latest product zip from the BuildAll output and unzips to your BranchBinariesDirectory
.Description
    The binaries will be loaded into your branch binaries directory. e.g. <your_branch_source>\Binaries
#>
function Get-ProductZip([switch]$unstable) {
    Write-Host "Getting latest product zip from [$ShellContext.BranchServerDirectory]"
    $zipName = "ExpertBinaries.zip"
    [string]$pathToZip = (PathToLatestSuccessfulPackage -pathToPackages $ShellContext.BranchServerDirectory -packageZipName $zipName -unstable $unstable)

    if (-not $pathToZip) {
        return
    }

    Write-Host "Selected " $pathToZip

    $pathToZip = $pathToZip.Trim()
    DeleteContentsFromExcludingFile -directory $BranchBinariesDirectory "environment.xml"
    Copy-Item -Path $pathToZip -Destination $BranchBinariesDirectory
    $localZip = (Join-Path $BranchBinariesDirectory $zipName)
    Write-Host "About to extract zip to [$BranchBinariesDirectory]"
    
    $zipExe = join-path $ShellContext.BuildToolsDirectory "\7z.exe"
    if (test-path $zipExe) {
        $SourceFile = $localZip
        $Destination = $BranchBinariesDirectory
        &$zipExe x $SourceFile "-o$Destination" -y
    } else {
        Write-Host "Falling back to using Windows zip util as 7-zip does not exist on this system"
        $shellApplication = new-object -com shell.application
        $zipPackage = $shellApplication.NameSpace($localZip)
        $destinationFolder = $shellApplication.NameSpace($ShellContext.BranchBinariesDirectory)
        $destinationFolder.CopyHere($zipPackage.Items())     
    }
    
    Write-Host "Finished extracting zip"
    [string]$versionFilePath = Join-Path $ShellContext.BranchBinariesDirectory "BuildAllZipVersion.txt"
    echo $pathToZip | Out-File -FilePath $versionFilePath
}

<#
.Synopsis
    Runs a GetProduct for the current branch but will not contain the pdb's
.Description
    Uses the expertmanifest from the local Build.Infrastructure\Src\Package directory.
    No pdb's returned
    The binaries will be loaded into your branch binaries directory. e.g. <your_branch_source>\Binaries
#>
function Get-ProductNoDebugFiles {
    $shell = ".\GetProduct.ps1 -ProductManifestPathPath $ShellContext.ProductManifestPath -dropRoot $ShellContext.BranchServerDirectory -binariesDirectory $ShellContext.BranchBinariesDirectory -systemMapConnectionString (Get-SystemMapConnectionString)"
    pushd $ShellContext.PackageScriptsDirectory
    invoke-expression $shell | Out-Host
    popd
}

<#
.Synopsis 
    Displays the BuildAll version in the binaries directory of the current branch.
.Description
    WARNING: If you have done a Get-Product or Get-ProductZip since your last deployment, then it will show the version number of the Get-ProductZip rather than what is deployed.
.PARAMETER copyToClipboard
    If specified the BuildAll version will be copied to the clipboard.
#>
function Get-ProductBuild([switch]$copyToClipboard) {
    [string]$versionFilePath = "$($ShellContext.BranchBinariesDirectory)\BuildAllZipVersion.txt"
    if ([System.IO.File]::Exists($versionFilePath)) {
        if ((Get-Content -Path $versionFilePath) -match "[^\\]*[\w.]BuildAll_[\w.]*[^\\]") {
            Write-Host "Current BuildAll version in $ShellContext.BranchName` branch:`r`n"

            if ($copyToClipboard) {
                Add-Type -AssemblyName "System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
                [System.Windows.Forms.Clipboard]::SetText($Matches[0])
            }

            return $Matches[0]
        } else {
            Write-Error "Content of BuildAllZipVersion.txt is questionable."
        }
    } else {
        Write-Error "No BuildAllZipVersion.txt present in: $($ShellContext.BranchBinariesDirectory)."
    }

    return $null
}