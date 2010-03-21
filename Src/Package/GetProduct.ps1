##
# For each module in the product manifest we gets all built <ModuleName>\Bin\Module from the $dropRoot and puts
# it into the $binariesDirectory. The factory .bin will be created from what exists in the $binariesDirectory
#
# Flag $getDebugFiles will get all pdb's so that you can debug
##
<# 
.Synopsis 
    Pull from the drop location all source and assocated tests of those modules that are defined in the given product manifest  
.Description    
    For each module in the product manifest we get the built ouput from <ModuleName>\Bin\Test and <ModuleName>\Bin\Module 
    and puts it into the $testBinariesDirectory. 
    The factory .bin will be created from what exists in the $testBinariesDirectory
.Example     
    GetProduct -$productManifestPath C:\Source\Dev\<branch name>\ExpertManifest.xml -$dropRoot \\na.aderant.com\expertsuite\dev\<branch name> -$binariesDirectory C:\Source\Dev\<branch name>\Binaries
    GetProduct -$productManifestPath C:\Source\Dev\<branch name>\ExpertManifest.xml -$dropRoot \\na.aderant.com\expertsuite\dev\<branch name> -$binariesDirectory C:\Source\Dev\<branch name>\Binaries -$getDebugFiles $true
.Parameter $productManifestPath is the path to the product manifest that defines the modules that makeup the product
.Parameter $dropRoot is the path drop location that the binaries will be fetched from
.Parameter $binariesDirectory the driectory you want the binaries to be copied too
#>
param( [string]$productManifestPath, [string]$dropRoot, [string]$binariesDirectory, [bool]$getDebugFiles=$false, [string]$buildLibrariesPath)

begin{  
    write "GetProduct.ps1"        
    [xml]$product =  Get-Content $productManifestPath    
    
    ###
    # Get the common Build-Libraries
    ###
    Function LoadLibraries(){            
        if([String]::IsNullOrEmpty($buildLibrariesPath)){
            $shell = (Join-Path $dropRoot \Build.Infrastructure\Src\Build\Build-Libraries.ps1 )
        }else{            
            $shell = (Join-Path $buildLibrariesPath \Build-Libraries.ps1 )
        }
        &($shell)
    }
    
    Function global:GenerateFactory([string]$inDirectory, [string]$searchPath){
        write "Generating factory in [$inDirectory]"        
        &$inDirectory\FactoryResourceGenerator.exe /f:$inDirectory /of:$inDirectory/Factory.bin $searchPath                        
    }
    
    Function global:GenerateSiteMap([string]$inDirectory, [string]$expertDatabaseConnectionString){
        write "Generating customisation sitemap"        
        &$inDirectory\Systemmapbuilder.exe /f:$inDirectory /o:$inDirectory/systemmap.xml /ef:Customization $expertDatabaseConnectionString  
        
        #/pdbs:WSRMT000900 /pdbd:CMSNET75SP3 /pdbid:cmsdbo /pdbpw:cmsdbo                             
    }
}

process{    

    LoadLibraries

    $binariesDirectory = Resolve-Path $binariesDirectory
    
    DeleteContentsFrom $binariesDirectory 

    foreach($module in $product.ProductManifest.Modules.SelectNodes("Module")){        
        $debugMessage = "Getting Bin for" + $module.Name
        write $debugMessage        
        [string]$moduleBinariesDirectory = GetPathToBinaries $module $dropRoot
		if(Test-Path $moduleBinariesDirectory.Trim()){                                                    
        	CopyModuleBinariesDirectory $moduleBinariesDirectory.Trim() $binariesDirectory $getDebugFiles               
		}else{
			Throw "Failed trying to copy output for $moduleBinariesDirectory" 
		}	
    }
    
    RemoveReadOnlyAttribute $binariesDirectory
    GenerateFactory $binariesDirectory "/sp:Aderant*.dll`,*.exe"
}

end{
    $doneMessage = "Product "+ $product.ProductManifest.Name +" retrieved"
    write $doneMessage
}

