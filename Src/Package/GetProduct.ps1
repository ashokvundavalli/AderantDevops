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
    and puts it into the $binariesDirectory. 
    The factory .bin will be created from what exists in the $binariesDirectory
.Example     
    GetProduct -$productManifestPath C:\Source\Dev\<branch name>\ExpertManifest.xml -$dropRoot \\na.aderant.com\expertsuite\dev\<branch name> -$binariesDirectory C:\Source\Dev\<branch name>\Binaries
    GetProduct -$productManifestPath C:\Source\Dev\<branch name>\ExpertManifest.xml -$dropRoot \\na.aderant.com\expertsuite\dev\<branch name> -$binariesDirectory C:\Source\Dev\<branch name>\Binaries -$getDebugFiles $true
.Parameter $productManifestPath is the path to the product manifest that defines the modules that makeup the product
.Parameter $dropRoot is the path drop location that the binaries will be fetched from
.Parameter $binariesDirectory the directory you want the binaries to be copied too
.Parameter $systemMapConnectionString for creation of customization in the format of /pdbs:<server> /pdbd:<expertdatabase> /pdbid:<userid> /pdbpw:<userpassword>
#>
param( [string] $productManifestPath,
       [string] $dropRoot,
       [string] $binariesDirectory, 
       [bool]   $getDebugFiles=$false, 
       [string] $buildLibrariesPath, 
       [string] $systemMapConnectionString)

begin{  
    write "GetProduct.ps1"        
    [xml]$product =  Get-Content $productManifestPath    
    
    ###
    # Get the common Build-Libraries
    ###
    Function LoadLibraries([string]$buildInfrastructurePath){
        $buildLibrariesPath = Join-Path -Path $buildInfrastructurePath.Trim() -ChildPath \Build\Build-Libraries.ps1
        &($buildLibrariesPath)
    }
    
    Function ResolveBuildInfrastructurePath($buildLibrariesPath){
        [string]$buildInfrastructureSrcPath
        if([String]::IsNullOrEmpty($buildLibrariesPath) -eq $true){
            $buildInfrastructureSrcPath = (Join-Path $dropRoot "Build.Infrastructure\Src\")
        }else{
            $buildInfrastructureSrcPath = (Join-Path $buildLibrariesPath "..\")
        }                                 
        return $buildInfrastructureSrcPath
    }
    
    Function global:GenerateFactory([string]$inDirectory, [string]$searchPath){
        write "Generating factory in [$inDirectory]"        
        &$inDirectory\FactoryResourceGenerator.exe /f:$inDirectory /of:$inDirectory/Factory.bin $searchPath                        
    }
    
    ###
    # Optionally run function that will generate the customisation sitemap
    ###
    Function global:GenerateSystemMap([string]$inDirectory, [string]$systemMapConnectionString){
        Write-Debug "About to generate the sitemap"                        
        
        if($systemMapConnectionString.ToLower().Contains("/pdbs:") -and 
           $systemMapConnectionString.ToLower().Contains("/pdbd:") -and 
           $systemMapConnectionString.ToLower().Contains("/pdbid:") -and 
           $systemMapConnectionString.ToLower().Contains("/pdbpw:")){
           
            $connectionParts = $systemMapConnectionString.Split(" ")                                            
            Write-Debug "Connection is [$connectionParts]"
            
            &$inDirectory\Systemmapbuilder.exe /f:$inDirectory /o:$inDirectory\systemmap.xml /ef:Customization $connectionParts[0] $connectionParts[1] $connectionParts[2] $connectionParts[3]        
        }else{
            Write-Error "Connection string is invalid for use with systemmapbuilder.exe [$systemMapConnectionString]"
        }                                                              
    }
	
	###
	# Move files required for the prerequisite installer into the drop location
	###
	Function MoveApplicationServerPrerequisitesToFolder([string] $rootPath) {
		$prerequisitesDropPath = Join-Path $rootPath 'ApplicationServerPrerequisites'
        write "Moving app server prerequisite files to $prerequisitesDropPath"
		CreateDirectory $prerequisitesDropPath
		CreateDirectory $(Join-Path $prerequisitesDropPath 'Tools')
		CreateDirectory $(Join-Path $prerequisitesDropPath 'Installers')

		MoveItem $(Join-Path $rootPath 'PrerequisitesPowerShell\ExpertApplicationServer.ps1') $(Join-Path $prerequisitesDropPath 'ExpertApplicationServer.ps1')
		MoveItem $(Join-Path $rootPath 'NTRights.exe') $(Join-Path $prerequisitesDropPath 'Tools\NTRights.exe')
		MoveItem $(Join-Path $rootPath 'dotNetFx40_Full_x86_x64.exe') $(Join-Path $prerequisitesDropPath 'Installers\dotNetFx40_Full_x86_x64.exe')
		MoveItem $(Join-Path $rootPath 'WindowsServerAppFabricSetup_x64_6.1.exe') $(Join-Path $prerequisitesDropPath 'Installers\WindowsServerAppFabricSetup_x64_6.1.exe')
	}
    
}

process{    
    
    [string]$buildInfrastructurePath = ResolveBuildInfrastructurePath($buildLibrariesPath)
    
    LoadLibraries -buildInfrastructurePath $buildInfrastructurePath

    $binariesDirectory = Resolve-Path $binariesDirectory
    DeleteContentsFromExcludingFile $binariesDirectory "environment.xml"

    # Was having an issue with the ExpertSource folder not being removed correctly and causing issue when getting the dlls.
    # Sleep stops this from happening
    Start-Sleep -m 1500
    #Create ExpertSource and Deployment Folders
    $expertSourceDirectory = Join-Path $binariesDirectory 'ExpertSource'
    CreateDirectory $expertSourceDirectory


    foreach($module in $product.ProductManifest.Modules.SelectNodes("Module")){        
        $debugMessage = "Getting Bin for " + $module.Name
        write $debugMessage        
        [string]$moduleBinariesDirectory = GetPathToBinaries $module $dropRoot
		if(Test-Path $moduleBinariesDirectory.Trim()){                                                    
        	CopyModuleBinariesDirectory $moduleBinariesDirectory.Trim() $expertSourceDirectory $getDebugFiles               
		}else{
			Throw "Failed trying to copy output for $moduleBinariesDirectory" 
		}	
    }
    
    RemoveReadOnlyAttribute $binariesDirectory
    
	MoveApplicationServerPrerequisitesToFolder $expertSourceDirectory
	
    GenerateFactory $expertSourceDirectory "/sp:Aderant*.dll`,*.exe"    
    if([string]::IsNullOrEmpty($systemMapConnectionString) -ne $true){
        GenerateSystemMap $expertSourceDirectory $systemMapConnectionString
    }
        
    MoveDeploymentFilesV8 $binariesDirectory $expertSourceDirectory
    
}

end{
    $doneMessage = "Product "+ $product.ProductManifest.Name +" retrieved"
    write ""
    write $doneMessage
}
