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

param([string]$productManifestPath, [string]$dropRoot, [string]$binariesDirectory, [string]$buildLibrariesPath)
begin {  
    write "GetProductTests.ps1"
    if ([string]::IsNullOrWhiteSpace($productManifestPath)) {
        Write-Warning "Parameter productManifestpath was not specified."
    }
    if ([string]::IsNullOrWhiteSpace($dropRoot)) {
        Write-Warning "Parameter dropRoot was not specified."
    }
    if ([string]::IsNullOrWhiteSpace($binariesDirectory)) {
        Write-Warning "Parameter binariesDirectory was not specified."
    }
    if ([string]::IsNullOrWhiteSpace($buildLibrariesPath)) {
        Write-Warning "Parameter buildLibrariesPath was not specified."
    }

    [xml]$product =  Get-Content $productManifestPath          
    
    
    function WarnOnMissingConfigFiles([string]$path) {
        $files = Get-ChildItem $path -filter IntegrationTest*.dll

        foreach ($file in $files) {
            $config = $file.FullName + ".config"

            if (-not (Test-Path $config)) {
                Write-Warning "Missing integration test config file: $config"
            }    
        }
    }                   
}

process { 
    #Define ExpertSource Folder
    $expertSourceDirectory = Join-Path $binariesDirectory 'ExpertSource'

    $scriptPath = Join-Path $buildLibrariesPath '\..\GetProduct.ps1'
    
    & "$scriptPath" $productManifestPath $dropRoot $binariesDirectory $True $buildLibrariesPath
    
    #Only get test binaries for local modules.
    foreach($module in $product.ProductManifest.Modules.SelectNodes("Module[not(@GetAction)]")){                                      
        
        if ((IsThirdparty $module) -or (IsHelp $module)){            
            Write-Host "Skipping " $module.Name
            continue
        } else {
            [string]$testBinariesDirectory = ServerPathToModuleTestBinariesFor $module $dropRoot
            
            if ($testBinariesDirectory -eq $null) {
                Write-Host "Skipping " $module.Name
                continue
            }                        
        } 
        
        if (Test-Path $testBinariesDirectory) {
            CopyModuleBinariesDirectory $testBinariesDirectory $expertSourceDirectory
        } else {
            Write-Warning "$testBinariesDirectory does not exist!"
        }
    }         
            
    RemoveReadOnlyAttribute $binariesDirectory
        
    #Generate Factory Resource.    
    GenerateFactory $expertSourceDirectory "/sp:Aderant*.dll`,*Test*.dll`,*.exe"

    WarnOnMissingConfigFiles $expertSourceDirectory
}

end {
    $doneMessage = "Product "+ $product.ProductManifest.Name +" Tests retrieved"
    write $doneMessage
}

