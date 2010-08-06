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
    
    Function global:GenerateClickOnceManifest([string]$buildToolsPath, [string]$inDirectory){
    
        Write-Debug "Generating ClickOnce Manifests"
        $mageTool = (Join-Path $buildToolsPath -ChildPath "mage.exe")
        $certificate = (Join-Path $buildToolsPath -ChildPath "..\Build\aderant.pfx")
        $password = "rugby ball"
                        
        [Array]$applicationFiles += dir -Path $inDirectory -Recurse -Filter *.application
        
        if($applicationFiles -ne $null){               
            foreach($applicationFile in $applicationFiles){                          
                                                       
                $applicationFileName = $applicationFile.Name
                if($applicationFileName -match "CO.application"){
                    $fileName = $applicationFileName -replace "CO.application", ""
                }else{
                    $fileName = $applicationFileName -replace ".application", ""
                }                        
                $applicationPath = $applicationFile.FullName                                                        
                $exePath = $applicationPath -replace ".application" , ".exe"
                $exeManifestPath = $exePath -replace  ".exe", ".exe.manifest"                                                                        
                #sign first so that the hash is not invalidate for manifests
                SignExecutable -buildToolsPath $buildToolsPath -fileName $fileName -inDirectory $inDirectory
                # applied to the foo.exe.manifest
                $UpdateApplicationManifest = "$mageTool -Update $exeManifestPath"                    
                # applied to the fooCO.application
                $UpdateDeploymentManifest = "$mageTool -Update $applicationPath"                
                #run the application command first because the deployment manifest depends on this                                
                Write-Debug $UpdateApplicationManifest
                Invoke-Expression $UpdateApplicationManifest                                
                                
                Write-Debug $UpdateDeploymentManifest
                Invoke-Expression $UpdateDeploymentManifest
                                
                RemoveHashFromApplicationManifest -applicationFile $applicationPath            
            }
        }        
    }
    
    Function global:SignExecutable([string]$buildToolsPath, [string]$fileName, [string]$inDirectory){
    
        Write-Debug "Siging [$fileName]"
        $signTool = (Join-Path $buildToolsPath -ChildPath "signtool.exe")
        $key = (Join-Path $buildToolsPath -ChildPath "..\Build\aderant.pfx")   
        
        [Array]$pathToExecutables += dir -Path $inDirectory -Recurse -Filter $fileName*.exe 
                        
        if($pathToExecutables -ne $null){               
            foreach($exe in $pathToExecutables){       
            
            $pathToExe = $exe.FullName 
            
            Write-Debug "About to Sign [$pathToExe]"
            
            $signExeCommand = "$signTool sign /p `"rugby ball`" /f $key $pathToExe"                            
            Write-Debug $signExeCommand
            Invoke-Expression $signExeCommand      
            }
        }    
    }
    
    Function RemoveHashFromApplicationManifest([string]$applicationFile){    
        [xml]$applicationManifest  = Get-Content $applicationFile        
        $hashNode = $applicationManifest.assembly.dependency.dependentAssembly["hash"]
        $applicationManifest.assembly.dependency.dependentAssembly.RemoveChild($hashNode)  | Out-Null
        $applicationManifest.Save($applicationFile) | Out-Null        
    }
    
    
}

process{    
    
    [string]$buildInfrastructurePath = ResolveBuildInfrastructurePath($buildLibrariesPath)
    
    LoadLibraries -buildInfrastructurePath $buildInfrastructurePath

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
    if([string]::IsNullOrEmpty($systemMapConnectionString) -ne $true){
        GenerateSystemMap $binariesDirectory $systemMapConnectionString
    }
    
    $pathToBuildTools = Join-Path -Path $buildInfrastructurePath.Trim() -ChildPath "\Build.Tools"
    GenerateClickOnceManifest -buildToolsPath $pathToBuildTools  -inDirectory $binariesDirectory
}

end{
    $doneMessage = "Product "+ $product.ProductManifest.Name +" retrieved"
    write ""
    write $doneMessage
}

