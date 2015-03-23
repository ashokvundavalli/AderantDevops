<# 
.Synopsis 
    Pull from the drop location all source and assocated tests of those modules that are defined in the given product manifest  
.Description    
    For each module in the product manifest we get the built ouput from <ModuleName>\Bin\Test and <ModuleName>\Bin\Module 
    and puts it into the $binariesDirectory. 
    The factory .bin will be created from what exists in the $binariesDirectory
.Example     
    GetProductTests -$productManifestPath C:\Source\Dev\<branch name>\ExpertManifest.xml -$dropRoot \\na.aderant.com\expertsuite\dev\<branch name> -$binariesDirectory C:\Source\Dev\<branch name>\Binaries
.Parameter $productManifestPath is the path to the product manifest that defines the modules that makeup the product
.Parameter $dropRoot is the path drop location that the binaries will be fetched from
.Parameter $binariesDirectory the driectory you want the binaries to be copied too
#> 
param( [string]$buildSourcesDirectory, [string]$productManifestPath, [string]$dropRoot, [string]$binariesDirectory, [string]$buildLibrariesPath)

begin{  
    write "GetProductTests.ps1"        
    [xml]$product =  Get-Content $productManifestPath                             
}

process{ 
    #Define ExpertSource Folder
    $expertSourceDirectory = Join-Path $binariesDirectory 'ExpertSource'

    $scriptPath = Join-Path $buildSourcesDirectory '\GetProduct.ps1'
    
	& "$scriptPath" $productManifestPath $dropRoot $binariesDirectory $True $buildLibrariesPath
    
    #Only get test binaries for local modules.
    foreach($module in $product.ProductManifest.Modules.SelectNodes("Module[not(@GetAction)]")){                                      
        
        if((IsThirdparty $module) -or (IsHelp $module)){            
            $skipMessage = "Skipping " + $module.Name
            write $skipMessage                      
        }else{
            [string]$testBinariesDirectory = ServerPathToModuleTestBinariesFor $module $dropRoot            
        } 
        
        if(Test-Path $testBinariesDirectory){
            CopyModuleBinariesDirectory $testBinariesDirectory $expertSourceDirectory
        }else{
            $noBinMessage = "Warning - $testBinariesDirectory does not exist!"                           
            write $noBinMessage
        }
    }         
            
    RemoveReadOnlyAttribute $binariesDirectory
        
    #Generate Factory Resource.    
    GenerateFactory $expertSourceDirectory "/sp:Aderant*.dll`,*Test*.dll`,*.exe"
}

end{
    $doneMessage = "Product "+ $product.ProductManifest.Name +" Tests retrieved"
    write $doneMessage
}

