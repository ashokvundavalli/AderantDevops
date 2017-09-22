<#
.Synopsis 
    Pull from the drop location all source and assocated tests of those modules that are defined in the given product manifest  
.Description    
    For each module in the product manifest we get the built ouput from <ModuleName>\Bin\Test and <ModuleName>\Bin\Module 
    and puts it into the $binariesDirectory. 
    The factory .bin will be created from what exists in the $binariesDirectory
.Example     
    GetProductTests -$productManifestPath C:\Source\Dev\<branch name>\ExpertManifest.xml -$dropRoot \\na.aderant.com\expertsuite\dev\<branch name> -$binariesDirectory C:\Source\Dev\<branch name>\Binaries

    For a local developer workstation

    cd $PackageScriptsDirectory
    .\GetProductTests.ps1 -productManifestPath $productManifest -dropRoot $env:ExpertdropRootUNCPath -binariesDirectory $BranchBinariesDirectory -buildLibrariesPath $BuildScriptsDirectory

.Parameter $productManifestPath is the path to the product manifest that defines the modules that makeup the product
.Parameter $dropRoot is the path drop location that the binaries will be fetched from
.Parameter $binariesDirectory the driectory you want the binaries to be copied too
.Parameter $buildLibrariesPath the driectory which contains Build-Libraries.ps1
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$productManifestPath,
    [Parameter(Mandatory=$true)][string]$dropRoot,
    [Parameter(Mandatory=$true)][string]$binariesDirectory,
    [Parameter(Mandatory=$false)][boolean]$getDebugFiles = $false,
    [Parameter(Mandatory=$false)][string]$systemMapConnectionString,
    [Parameter(Mandatory=$false)][switch]$onlyUpdated,
    [Parameter(Mandatory=$true)][string]$teamProject,
    [Parameter(Mandatory=$true)][string]$tfvcBranchName,
    [Parameter(Mandatory=$true)][string]$tfvcSourceGetVersion,
    [Parameter(Mandatory=$true)][string]$buildUri,
    [Parameter(Mandatory=$true)][string]$tfsBuildNumber
)

begin {  
    Write-Host "GetProductTests.ps1"
    if ([string]::IsNullOrWhiteSpace($productManifestPath)) {
        Write-Error "Parameter productManifestpath was not specified."
        exit 1
    }
    if ([string]::IsNullOrWhiteSpace($dropRoot)) {
        Write-Error "Parameter dropRoot was not specified."
        exit 1
    }
    if ([string]::IsNullOrWhiteSpace($binariesDirectory)) {
        Write-Error "Parameter binariesDirectory was not specified."
        exit 1
    }

    Write-Host "Expert Manifest: $productManifestPath"
    Write-Host "Drop Root: $dropRoot"
    Write-Host "Binaries Directory: $binariesDirectory"
    Write-Host "Package Libraries: $packagePath"

    [xml]$product = Get-Content $productManifestPath
    
    function WarnOnMissingConfigFiles([string]$path) {
        $files = Get-ChildItem $path -filter UIAutomation*.dll

        foreach ($file in $files) {
            $config = "$($file.FullName).config"

            if (-not (Test-Path $config)) {
                Write-Warning "Missing UIAutomation test config file: $($config)"
            }
        }
    }
}

process { 
    # Define ExpertSource Folder
    [string]$expertSourceDirectory = Join-Path -Path $binariesDirectory -ChildPath "ExpertSource"
    # GetProduct.ps1 path
    [string]$getProduct = Join-Path -Path $PSScriptRoot -ChildPath "GetProduct.ps1"

    if (-not (Test-Path $getProduct)) {
        Write-Error "Unable to locate GetProduct.ps1 at path: $($getProduct)"
        exit 1
    }

    . $getProduct -productManifestPath $productManifestPath -dropRoot $dropRoot -binariesDirectory $binariesDirectory -getDebugFiles $getDebugFiles -systemMapConnectionString '$systemMapConnectionString' -onlyUpdated:$onlyUpdated.IsPresent -teamProject $teamProject -tfvcBranchName $tfvcBranchName -tfvcSourceGetVersion $tfvcSourceGetVersion -buildUri $buildUri -tfsBuildNumber $tfsBuildNumber

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    # Only get test binaries for local modules.
    foreach ($module in $product.ProductManifest.Modules.SelectNodes("Module[not(@GetAction)]")) {
        [string]$testBinariesDirectory

        if ((IsThirdparty $module) -or (IsHelp $module)) {            
            Write-Host "Skipping $($module.Name)"
            continue
        } else {
            if ($module -eq $null -or $dropRoot -eq $null -or $module.ExcludeFromPackaging -eq $true) {
                Write-Host "Skipping $($module.Name)"
                continue
            }

            $testBinariesDirectory = ServerPathToModuleTestBinariesFor $module $dropRoot
        }

        if ($testBinariesDirectory -eq $null) {
            Write-Host "Skipping $($module.Name)"
            continue
        }

        if (-not (Test-Path $testBinariesDirectory)) {
            Write-Warning "Skipping $($module.Name)"
            Write-Warning "$testBinariesDirectory does not exist"
            continue
        }
        
        CopyModuleBinariesDirectory $testBinariesDirectory $expertSourceDirectory
    }

    RemoveReadOnlyAttribute $binariesDirectory
    WarnOnMissingConfigFiles $expertSourceDirectory
}

end {
    Write-Host "Product $($product.ProductManifest.Name) tests retrieved"
    exit $LASTEXITCODE
}