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
.Parameter $onlyUpdated only gets the modules that have changed since the last get-product
#>
param([string]$productManifestPath,
      [string]$dropRoot,
      [string]$binariesDirectory, 
      [bool]  $getDebugFiles=$false, 
      [string]$buildLibrariesPath, 
      [string]$systemMapConnectionString,
      [switch]$onlyUpdated,
      [switch]$Elite) 

begin {  
    write "GetProduct.ps1"        
    [xml]$product =  Get-Content $productManifestPath    
    
    ###
    # Get the common Build-Libraries
    ###
    Function LoadLibraries([string]$buildInfrastructurePath) {
        $buildLibrariesPath = [System.IO.Path]::Combine($buildInfrastructurePath, "Build-Libraries.ps1")
        if (-not (Test-Path $buildLibrariesPath)) {
            $buildLibrariesPath = [System.IO.Path]::Combine($buildInfrastructurePath, "Build", "Build-Libraries.ps1")
        }                    

        Write-Host "Loading $buildLibrariesPath"
      
        &($buildLibrariesPath)
    }
    
    Function ResolveBuildInfrastructurePath($buildLibrariesPath){
        [string]$buildInfrastructureSrcPath = $null
        if ([String]::IsNullOrEmpty($buildLibrariesPath) -eq $true){
            $buildInfrastructureSrcPath = (Join-Path $dropRoot "Build.Infrastructure\Src\")
        } else {
            return $buildLibrariesPath
        }
                                       
        return $buildInfrastructureSrcPath        
    }
    
    Function global:GenerateFactory([string]$inDirectory, [string]$searchPath){
        write "Generating factory in [$inDirectory]"        
        &$inDirectory\FactoryResourceGenerator.exe /f:$inDirectory /of:$inDirectory/Factory.bin $searchPath                        
    }
    
    ###
    # Optionally run function that will generate the customization sitemap
    ###
    Function global:GenerateSystemMap([string]$inDirectory, [string]$systemMapArgs){
        Write-Debug "About to generate the sitemap"                        
        
        if ($systemMapArgs -like "*/pdbs*" -and $systemMapArgs -like "*/pdbd*" -and $systemMapArgs -like "*/pdbid*" -and $systemMapArgs -like "*/pdbpw*") {           
            $connectionParts = $systemMapArgs.Split(" ")                                            
            Write-Host "SystemMap connection is [$connectionParts]"
           
            &$inDirectory\Systemmapbuilder.exe /f:$inDirectory /o:$inDirectory\systemmap.xml /ef:Customization $connectionParts[0] $connectionParts[1] $connectionParts[2] $connectionParts[3]        
        } else {
            throw "Connection string is invalid for use with SystemMapBuilder.exe [$systemMapArgs]"
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
        MoveItem $(Join-Path $rootPath 'NDP451-KB2858728-x86-x64-AllOS-ENU.exe') $(Join-Path $prerequisitesDropPath 'Installers\NDP451-KB2858728-x86-x64-AllOS-ENU.exe')
        MoveItem $(Join-Path $rootPath 'WindowsServerAppFabricSetup_x64.exe') $(Join-Path $prerequisitesDropPath 'Installers\WindowsServerAppFabricSetup_x64.exe')
        MoveItem $(Join-Path $rootPath 'rewrite_amd64_en-US.msi') $(Join-Path $prerequisitesDropPath 'Installers\rewrite_amd64_en-US.msi')
    }
}

process {
    [string]$buildInfrastructurePath = ResolveBuildInfrastructurePath($buildLibrariesPath)
    
    #Get the Elite exclusion manifest.
    $index = $productManifestPath.LastIndexOf('\');
    $excludeManifestPath = Join-Path $productManifestPath.Substring(0, $index) EliteExclusionManifest.xml
    [xml]$excludeManifest = Get-Content $excludeManifestPath

    $newdropFolderBuildNumbers = @()

    LoadLibraries -buildInfrastructurePath $buildInfrastructurePath
    
    if(Test-Path "$binariesDirectory\DropFolderBuildNumbers.txt"){
        $dropFolderBuildNumbers = Get-Content "$binariesDirectory\DropFolderBuildNumbers.txt"
    }

    if((-not $onlyUpdated) -or ($dropFolderBuildNumbers -eq $null)){
        if(Test-Path $binariesDirectory){                
            Remove-Item $binariesDirectory\* -Recurse -Force -Exclude "environment.xml"
        }
    }else{
        if(Test-Path $binariesDirectory){                
            Remove-Item $binariesDirectory\* -Force -Exclude "environment.xml","DropFolderBuildNumbers.txt", ExpertSource
        }
    }

    if(-not $onlyUpdated){
    # Was having an issue with the ExpertSource folder not being removed correctly and causing issue when getting the dlls.
    # Sleep stops this from happening
    Start-Sleep -m 1500
    }

    #Create ExpertSource and Deployment Folders
    $expertSourceDirectory = Join-Path $binariesDirectory 'ExpertSource'
    if(-not $onlyUpdated){
    CreateDirectory $expertSourceDirectory
    }

    foreach ($module in $product.SelectNodes("//ProductManifest/Modules/Module[not(@ExcludeFromPackaging)]")) {
        $debugMessage = "Getting Bin for " + $module.Name
        write $debugMessage        
        [string]$moduleBinariesDirectory = GetPathToBinaries $module $dropRoot

        # If onlyUpdated switch is entered we only copy modules that have changed since last time we copied them
        if($onlyUpdated){
            $areBinariesUpToDate = $false
            for($i = 0; $i -lt $dropFolderBuildNumbers.Length; $i++){
                if($dropFolderBuildNumbers[$i].StartsWith($module.Name)){
                    if((IsThirdParty $module) -or ($module.Name.Contains("Help"))){
                        # Check the date for ThirdParty modules
                        $modifiedDate = ((Get-Item $moduleBinariesDirectory).LastWriteTimeUtc).ToString("o")
                        if($modifiedDate -like $dropFolderBuildNumbers[$i].Split("=")[1]){
                            Write-Host -ForegroundColor Gray "Skipping " $module.Name " as it is up to date."
                            $newdropFolderBuildNumbers += $dropFolderBuildNumbers[$i]
                            $areBinariesUpToDate = $true
                            break
                        }
                    }else{
                        try{
                            # Check the drop build number for Aderant modules
                            $dropBuildNumber = LatestSuccesfulBuildNumber $module $dropRoot
                        }catch{
                            $dropBuildNumber = ((Get-Item $moduleBinariesDirectory).LastWriteTimeUtc).ToString("o")
                        }
                        if($dropBuildNumber -like $dropFolderBuildNumbers[$i].Split("=")[1]){
                            Write-Host -ForegroundColor Gray "Skipping " $module.Name " as it is up to date."
                            $newdropFolderBuildNumbers += $dropFolderBuildNumbers[$i]
                            $areBinariesUpToDate = $true
                            break
                        }
                    }
                }
            }

            if($areBinariesUpToDate -and -not $module -eq "Libraries.Roles"){
                continue
            }
        }
        
        $OnlyInElite = @() #Things that should be excluded in expert and included in elite.
        $InBoth = @() #Things that should be included in expert and elite. All unmentioned files are excluded by default.

        $nodes = $excludeManifest.SelectNodes('ExclusionManifest[@name="Elite"]/Module[@name="'+$module.Name+'"]') #///Module[@name="+$module.Name+"]"
        if ($nodes -ne $null) {
            if ($Elite -and $nodes.action -eq "Exclude") {
                #Exclude / skip the whole module.
                continue;
            }
            try {
                $OnlyInEliteItems = $excludeManifest.SelectNodes('ExclusionManifest[@name="Elite"]/Module[@name="'+$module.Name+'"]/OnlyInElite/*')
                if ($OnlyInEliteItems -ne $null) {
                    foreach ($item in $OnlyInEliteItems) {
                        $OnlyInElite += $item.path
                    }
                }
            } catch {
                #There was no OnlyInElite section.
            }
            if ($Elite) {
                try {
                    $InBothItems = $excludeManifest.SelectNodes('ExclusionManifest[@name="Elite"]/Module[@name="'+$module.Name+'"]/InBoth/*')
                    if ($InBothItems -ne $null) {
                        foreach ($item in $InBothItems) {
                            $InBoth += $item.path
                        }
                    }
                } catch {
                    #There was no InBoth section.
                }
            }
        }

        if (Test-Path $moduleBinariesDirectory.Trim()) {
            CopyModuleBinariesDirectory $moduleBinariesDirectory.Trim() $expertSourceDirectory $getDebugFiles $Elite          
        } else {
            Throw "Failed trying to copy output for $moduleBinariesDirectory" 
        }	

        if((IsThirdParty $module) -or ($module.Name.Contains("Help"))) {
            $newdropFolderBuildNumbers += $module.Name + "=" + ((Get-Item $moduleBinariesDirectory).LastWriteTimeUtc).ToString("o")
        } else {
            try {
                $newdropFolderBuildNumbers += $module.Name + "=" + (LatestSuccesfulBuildNumber $module $dropRoot)
            } catch {
                $newdropFolderBuildNumbers += $module.Name + "=" + ((Get-Item $moduleBinariesDirectory).LastWriteTimeUtc).ToString("o")
            }
        }

        if ($nodes -ne $null) {
            $allFilesPath = $moduleBinariesDirectory.Trim() + '\*'
            $files = Get-Item $allFilesPath
            $deleteList = @()
            if ($Elite) {
                foreach ($item in $files) {
                    $name = $item.Name
                    if (-not ($InBoth.Contains($name) -or $OnlyInElite.Contains($name))) {
                        $deleteList += (Join-Path $expertSourceDirectory $name)
                    }
                }
            } else {
                foreach($item in $OnlyInElite) {
                    $deleteList += (Join-Path $expertSourceDirectory $item)
                }
            }
            foreach($item in $deleteList) {
                write ('Excluding File: ' + $item)
                rm $item
            }
        }	

    }
    
    RemoveReadOnlyAttribute $binariesDirectory
    
    MoveApplicationServerPrerequisitesToFolder $expertSourceDirectory
    
    GenerateFactory $expertSourceDirectory "/sp:Aderant*.dll`,*.exe"    
    if ([string]::IsNullOrEmpty($systemMapConnectionString) -ne $true) {
        GenerateSystemMap $expertSourceDirectory $systemMapConnectionString
    }
        
    MoveDeploymentFiles $product.ProductManifest.ExpertVersion $binariesDirectory $expertSourceDirectory
    MoveInternalFiles $product.ProductManifest.ExpertVersion $expertSourceDirectory
    
    $newdropFolderBuildNumbers | Out-File $binariesDirectory\DropFolderBuildNumbers.txt
}

end {
    $doneMessage = "Product "+ $product.ProductManifest.Name +" retrieved"
    write ""
    write $doneMessage
}
