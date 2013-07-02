<# 
.Synopsis 
    Functions relating to the modules 
.Example     
        
.Remarks
#>    
    
    ###
    # Loads the local dependency manifest 
    ###
    Function global:LoadManifest([string]$manifestPath){                
        $path = Join-Path -Path $manifestPath -ChildPath "DependencyManifest.xml"
        if (Test-Path $path) {
            return Get-Content $path -Force
        }
        Write-Warning "No dependency manifest exists at $manifestPath"
    }  
    
    ###
    # Loads the branch expert manifest
    ###
    Function global:LoadExpertManifest([string]$buildScriptsDirectory){                
        return Get-Content ($buildScriptsDirectory + "\..\Package\ExpertManifest.xml")
    }                                 
               
    ###
    # Get a copy of the required build scripts for this module
    ###
    Function global:CopyModuleBuildFiles([string]$fromPath, [string]$toPath){
              
        $fromPath =  Join-Path $fromPath "\Build.Infrastructure\Src\Build\"                          
        
        $buildFiles = Get-ChildItem $fromPath -Exclude "LoadDependancies.ps1"
        $buildFiles | 
        ForEach-Object {
            Copy-Item -Path $_.FullName -Destination $toPath -Force
        }                        
    }      
    
    <#
    .Synopsis
        Finds the module within the Product Manifest
    .Description
        Performs a case insenstive search for a module
    .Parameter modules
        The Modules node of the Product Manifest
    .Parameter name
        The name of the module
    #>
    Function global:FindModuleFromManifest([System.Xml.XmlNode]$modules, [string]$name) {	
        Write-Debug "Looking for module $name"
        
        $name = $name.ToLowerInvariant()
        return [System.Xml.XmlNode]$modules.SelectSingleNode("Module[translate(@Name, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz') = '$name']")
    }           
        
    ##
    # Resolves the path to the binaries for the given module
    ##
    Function global:GetPathToBinaries([System.Xml.XmlNode]$module, [string]$dropPath){
                       
        $action = FindGetActionTag $module            
        Switch ($action)
        {
          "local"  { LocalPathToModuleBinariesFor $module }
          "local-external-module"  { LocalPathToThirdpartyBinariesFor $module }
          "current-branch-external-module"  { ThirdpartyBinariesPathFor  $module $dropPath $action}          
          "other-branch-external-module"  { ThirdpartyBinariesPathFor  $module $dropPath $action}       
          "other-branch"  { ServerPathToModuleBinariesFor $module $dropPath $action}
          "current-branch"  { ServerPathToModuleBinariesFor $module $dropPath $action}
          Default { throw "invalid action [$action]"}
        }
    }                 
    
    ##
    # Resolves the path to the binaries for the given module
    ##
    Function global:GetLocalPathToBinaries([System.Xml.XmlNode]$module, [string]$localPath){
    
        if((IsThirdParty $module) -or (IsHelp $module)){
            return LocalPathToThirdpartyBinariesFor $module $localPath
        }else{                    
            return LocalPathToModuleBinariesFor $module $localPath                    
        }
    }
    
    ##
    # Find the get action for this module
    ##
    Function global:FindGetActionTag([System.Xml.XmlNode]$module){
    
        [bool]$getModuleLocally=$false
        [bool]$getModuleFromAnotherBranch=$false        
    
        if($module.HasAttribute("GetAction")){            
            if($module.GetAction.ToLower().Equals("branch")){
                $getModuleFromAnotherBranch=$true
            }elseif($module.GetAction.ToLower().Equals("local")){
                $getModuleLocally=$true
            }            
        }        
                    
        if($getModuleFromAnotherBranch){                                
            if((IsThirdparty $module) -or (IsHelp $module)){
                return "other-branch-external-module"
            }
            return "other-branch"            
        }elseif($getModuleLocally){
            if((IsThirdparty $module) -or (IsHelp $module)){
                return "local-external-module"
            }
            return "local"        
        }else{
            if((IsThirdparty $module) -or (IsHelp $module)){
                return "current-branch-external-module"
            }
            return "current-branch"            
        }     
    }
        
    ##
    # Change and Test the drop path to a new branch
    ##
    Function global:ChangeBranch([string]$dropPath, [string]$branchName){
        
        $branchName = $branchName.ToLower()
        $dropPath = $dropPath.ToLower()
        
        if($dropPath.ToLower().Contains("main")){        
            $start = $dropPath.Substring(0,$dropPath.LastIndexOf("expertsuite\"))        
        }elseif($dropPath.ToLower().Contains("dev")){
            $start = $dropPath.Substring(0,$dropPath.LastIndexOf("dev\"))
        }elseif($dropPath.ToLower().Contains("release")){        
            $start = $dropPath.Substring(0,$dropPath.LastIndexOf("releases\"))
        }
   
        $changedRoot = (Join-Path $start ('\'+$branchName))  
        
        if(Test-Path $changedRoot -ErrorAction 1){
            return $changedRoot
        }else{        
            Throw(New-Object System.IO.DirectoryNotFoundException "path to branch [$changedRoot] is invalid")
        }                                          
    }
    
    ###
    # Is this a thirdparty module?
    ###
    Function global:IsThirdparty($module){
        $name = $null

        if ($module.GetType().FullName -like "System.Xml*") {
            $name = $module.Name
        } else {		
            $name = $module
        }
            
        return $name -like "thirdparty.*"
    }
    
    ###
    # Is this the help module?
    ###
    Function global:IsHelp([System.Xml.XmlNode]$module){
        return $module.Name.ToLower().Contains(".help")
    }                                   
        
    ##
    # Local binaries path
    ##
    Function global:LocalPathToModuleBinariesFor([System.Xml.XmlNode]$module, [string]$localPath){            
        $localModulePath = Join-Path -Path (Join-Path -Path $localPath -ChildPath $module.Name ) -ChildPath '\Bin\Module'    
        return $localModulePath
    }
    
    ##
    # Local thirdparty binaries path
    ##
    Function global:LocalPathToThirdpartyBinariesFor([System.Xml.XmlNode]$module, [string]$localPath){                    
        $localThirdpartyModulePath = Join-Path -Path (Join-Path -Path $localPath -ChildPath $module.Name ) -ChildPath '\Bin'        
        return $localThirdpartyModulePath
    }
    
    ##
    # Thirdparty binaries path from the drop
    ##
    Function global:ThirdpartyBinariesPathFor([System.Xml.XmlNode]$module, [string]$dropPath, [string]$action="current-branch-external-module"){                                                   
        if(!$dropPath){$rootPath=(Get-DropRootPath)}else{$rootPath=$dropPath}   
        
        if($action.Equals("other-branch-external-module") -and ![string]::IsNullOrEmpty($module.Path)) {
            $rootPath = ChangeBranch $rootPath $module.Path                    
        } 
             
        return (Join-Path $rootPath  ($module.Name+'\Bin'))
    }                                                           
    
    ##
    # Versioned binaries path from the drop
    ##
    Function global:ServerPathToModuleBinariesFor([System.Xml.XmlNode]$module, [string]$dropPath, [string]$action="current-branch"){                                            
        if(!$dropPath){$rootPath=(Get-DropRootPath)}else{$rootPath=$dropPath}         
                
        if($action.Equals("other-branch") -and ![string]::IsNullOrEmpty($module.Path)) {
            $rootPath = ChangeBranch $rootPath $module.Path                    
        }                                                       
        
        $binModule = '\Bin\Module'
        
        $pathToModuleAssemblyVersion = Join-Path -Path (Join-Path $rootPath $module.Name) -ChildPath $module.AssemblyVersion 

        
        
        if ($module.HasAttribute("FileVersion")){
            $modulePath = Join-Path -Path( Join-Path -Path $pathToModuleAssemblyVersion  -ChildPath $module.FileVersion) -ChildPath $binModule
        } else {           
        

            $modulePath = PathToLatestSuccessfulBuild $pathToModuleAssemblyVersion
        }


        
               
        return  $modulePath                                                
    }
    
    ##
    # Versioned test binaries path from the drop
    ##
    Function global:ServerPathToModuleTestBinariesFor([System.Xml.XmlNode]$module, [string]$dropPath){                                            
        if(!$dropPath){$rootPath=(Get-DropRootPath)}else{$rootPath=$dropPath}         
        
        $pathToModuleAssemblyVersion = Join-Path -Path (Join-Path -Path $rootPath -ChildPath $module.Name) -ChildPath $module.AssemblyVersion 
        
        if($module.HasAttribute("FileVersion")){
            $testBinPath = Join-Path -Path (Join-Path -Path $pathToModuleAssemblyVersion  -ChildPath $module.FileVersion) -ChildPath '\Bin\Test'
        }else{                               
            [string]$latestSuccessfulPath = PathToLatestSuccessfulBuild $pathToModuleAssemblyVersion         
            $testBinPath = Join-Path -Path $latestSuccessfulPath -ChildPath '..\Test'
        }
        Write-Host "Server path to test binaries for module " $module.Name " is " $testBinPath.Trim() "."               
        return  [string]$testBinPath.Trim()       
    }          
        
    ###
    # Find the last successfully build in the drop location.
    ###
    Function global:PathToLatestSuccessfulBuild([string]$pathToModuleAssemblyVersion) {
        $sortedFolders = SortedFolders $pathToModuleAssemblyVersion
        [bool]$noBuildFound = $true        
        [string]$pathToLatestSuccessfulBuild = $null      

        foreach ($folderName in $sortedFolders) {      
            $buildLog = Join-Path -Path( Join-Path -Path $pathToModuleAssemblyVersion -ChildPath $folderName.Name ) -ChildPath "\BuildLog.txt"
            $pathToLatestSuccessfulBuild = Join-Path -Path( Join-Path -Path $pathToModuleAssemblyVersion -ChildPath $folderName.Name ) -ChildPath "\Bin\Module"
            [string]$buildFailed = $null                       
            
            if (Test-Path $buildLog){
                $buildFailed = Get-Content -Path $buildLog | where {$_.Contains("Build FAILED")} | Out-Null            
            }
            if ([string]::IsNullOrEmpty($buildFailed) -and (test-path $pathToLatestSuccessfulBuild)) {                                            
                return $pathToLatestSuccessfulBuild
            }
        }     
        
        if ($noBuildFound) {
            throw "no latest build found for $pathToModuleAssemblyVersion"
        }
    }    
    
    ###
    # Find the last successfully package (Build all and package) build in the drop location.
    ###
    Function global:PathToLatestSuccessfulPackage([string]$pathToPackages, [string]$packageZipName){
                
        $packagingFolders = (dir -Path $pathToPackages | 
        where {$_.PsIsContainer -and $_.name.Contains(".BuildAll")} | 
        Sort {$_.LastWriteTime } -Descending | 
        Select name  )                        
        
        [bool]$noBuildFound = $true
        [string]$pathToLatestSuccessfulPackage
        
        foreach ($folderName in $packagingFolders) {            
        
            $buildLog = (Join-Path -Path( Join-Path -Path $pathToPackages -ChildPath $folderName.Name ) -ChildPath "\BuildLog.txt")
            $pathToLatestSuccessfulPackage = (Join-Path -Path( Join-Path -Path $pathToPackages -ChildPath $folderName.Name ) -ChildPath $packageZipName)
            
            if ((CheckBuild $buildLog) -and (test-path $pathToLatestSuccessfulPackage)) {   
            
                if(Test-Path $pathToLatestSuccessfulPackage){
                    return $pathToLatestSuccessfulPackage
                }                                        
            }
        }     
        
        if($noBuildFound){
            Write-Error "No latest build found for [$pathToPackages]"
        }
    }
    
    ###
    # Pads each section of the folder name (which is in the format 1.8.3594.41082) with zeroes, so that an alpha sort
    # can be used because each section will now be of the same length.
    ###
    Function global:SortedFolders( [string]$parentFolder ) {
    
        $sortedFolders =  (dir -Path $parentFolder | 
                    where {$_.PsIsContainer} | 
                    Sort-Object {$_.name.Split(".")[0].PadLeft(4,"0")+"."+ $_.name.Split(".")[1].PadLeft(4,"0")+"."+$_.name.Split(".")[2].PadLeft(8,"0")+"."+$_.name.Split(".")[3].PadLeft(8,"0")+"." } -Descending  |
                    select name)
        
        return $sortedFolders
    
    }  
    
    ###
    # Delete the files contained in the directory excluding the file name provided
    ###
    Function global:DeleteContentsFromExcludingFile([string]$directory, [string]$excludeFile){
        if(Test-Path $directory){                
            Remove-Item $directory\* -Recurse -Force -Exclude $excludeFile
        }
    }                                 
    
    ###
    # Delete the files contained in the directory
    ###
    Function global:DeleteContentsFrom([string]$directory){
        if(Test-Path $directory){                
            Remove-Item $directory\* -Recurse -Force
        }
    }   
    
    ##
    # 
    ##
    Function global:RemoveReadOnlyAttribute($productDirectory){    
        Push-Location $productDirectory | attrib -R /S  
        Pop-Location
    }
        
    ##
    # Mimic the folder structure and files from one location to another.
    # Using RoboCopy for simplicity as Copy-Item wasn't giving us what we required
    ##
    Function global:CopyContents([string]$copyFrom, [string]$copyTo){ 
       if($copyFrom.EndsWith('\')){       
           $copyFrom = $copyFrom.Remove($copyFrom.LastIndexOf('\'))          
       }       
              
       if($copyTo.EndsWith('\')){
           $copyTo =  $copyTo.Remove($copyTo.LastIndexOf('\'))         
       }       
       if(!(Test-Path $copyTo)){
           New-Item -ItemType Directory -Path $copyTo
       }
                                           
       $cmd = "robocopy.exe $($copyFrom.Trim()) $($copyTo.Trim()) /E /XX /NJH /NJS /R:3 /W:5 /NP /NC /NS /NDL /MT /A-:R"
       Invoke-Expression $cmd | % { FormatCopyMessage $_ }
    }                
    
    ##
    # 
    ##
    Function global:CopyModuleBinariesDirectory([string]$from, [string]$to,[bool]$includePdbFiles){
        write "Copying $from to $to, include pdbs? [$includePdbFiles]"
        if ($includePdbFiles){            
            robocopy $from $to /XD service.tfsbuild* /S /XO /NJH /NJS /NP /NFL /NDL /MT /A-:R | % { FormatCopyMessage $_ }
        } else {
            robocopy $from $to /XD service.tfsbuild* /XF *.pdb /S /XO /NJH /NJS /NP /NFL /NDL /MT /A-:R | % { FormatCopyMessage $_ }
        }
    }     
    
    <#
    Copy only files from the bin\module and bin\test that are built as part of this module for the drop
    i.e. they are not dependencies
    #>
    Function global:CopyBinFilesForDrop([string]$modulePath, [string]$toModuleDropPath){       
       
       $dropBinModulePath = Join-Path $toModuleDropPath Bin\Module
       $binTestPath = Join-Path $modulePath Bin\Test
       $dropBinTestPath = Join-Path $toModuleDropPath Bin\Test       
       
       if(!(Test-Path $dropBinModulePath)){
           New-Item -ItemType Directory -Path $dropBinModulePath | Out-Null
       }
              
       ResolveAndCopyUniqueBinModuleContent -modulePath $modulePath -copyToDirectory $dropBinModulePath
       
       if (Test-Path $binTestPath){       
            if (!(Test-Path $dropBinTestPath)){
                New-Item -ItemType Directory -Path $dropBinTestPath | Out-Null
            }
            
            if ($copyTestDirectory) {
                Write-Host "Copying test directory due to test failue"
                CopyContents -copyFrom $binTestPath -copyTo $dropBinTestPath 
            } else {
                Write-Host "Copying integration test artifacts to drop"
				# *.dll* as we need both the .dll and the .config
				Get-ChildItem $modulePath -Recurse -Filter IntegrationTest*.dll* | % { Copy-Item $_.FullName $dropBinTestPath }
            }
            
            Write-Host "Copying test results"                
            Get-ChildItem $modulePath -Recurse -Filter *.trx | % { Copy-Item $_.FullName $dropBinTestPath }
        }            
    }	
    
    <#
    Copies only built files, i.e. excludes items that are dependancies, from Bin\Module
    #>
    Function global:ResolveAndCopyUniqueBinModuleContent([string]$modulePath, [string]$copyToDirectory){                
       
       $dependenciesPath = Join-Path $modulePath Dependencies
       $binPath = Join-Path $modulePath Bin\Module
       
       if(Test-Path $dependenciesPath){
              
           [Array]$uniqueItems += dir -Path $binPath -Recurse -Exclude( dir $dependenciesPath -Recurse | ForEach-Object {$_.Name}) |
           ForEach-Object {$_.FullName}
                  
           [string]$binModulePart = "Bin\Module\" 
           
           if($uniqueItems -ne $null){               
               foreach($file in $uniqueItems){                               
               
                    $fileName = (Split-Path -Leaf -Path $file)
                    $fromPath = (Split-Path -Parent -Path $file)
               
                    [bool]$hasFoldersInBinModule = $fromPath.IndexOf($binModulePart) -ne -1
               
                    if($hasFoldersInBinModule){       
                        $substringFromIndex = $fromPath.IndexOf($binModulePart)+$binModulePart.Length
                        $substringToIndex = $fromPath.Length -($fromPath.IndexOf($binModulePart)+$binModulePart.Length)            
                        $fileRelativePath = $fromPath.Substring($substringFromIndex, $substringToIndex)            
                        $toPath = (Join-Path $copyToDirectory $fileRelativePath)                                   
                    }else{
                        $toPath = $copyToDirectory
                    }                   
                    Robocopy.exe $fromPath $toPath $fileName /NP /XX /NJS /NJH /MT /NS /NS /NDL /NC /XF /A-:R *.pfx *.trx | % { FormatCopyMessage $_ }
               }
           }
       }else{
            Write-Host "No dependencies for $modulePath to be resolved, copying entire bin/module."
            Robocopy.exe $binPath $copyToDirectory /E /NP /NJS /NJH /MT /XF /NS /NS /NDL /NC /XF /A-:R *.pfx *.trx | % { FormatCopyMessage $_ }
       }       
    }
               
    <#
    Checks tail of a build log.  If build successful returns True.
    #>    
    Function global:CheckBuild([string]$buildLog){
                    
        if(Test-Path $buildLog){            
            $noErrors = Get-Content $buildLog | select -last 10 | where {$_.Contains("0 Error(s)")}
                        
            if ($noErrors){      
               return $true           
            }else{ 
               return $false
            }
        }else{
            Write-Warning "No build log to check at [$buildLog]"
        }
    }
    
    <#
    We now need to move/copy the deployment manager files depending on the version we are working on.  There are three different scenarios:
    1. 7SP2 and earlier - all files are in Binaries folder.
    2. 7SP4 - all deployment files listed in ..\Build.Infrastructure\Src\Package\deploymentManagerFilesList.txt are moved to Binaries\DeploymentManager folder
       see GetProduct.ps1 (Function MoveDeploymentManagerFilesToFoler) for details.
    3. 8 and later - all deployment files listed in ..\Build.Infrastructure\Src\Package\deploymentManagerFilesList.txt are moved to Binaries\Deployment folder.
    #> 
    Function global:MoveDeploymentFiles([string]$expertVersion, [string]$binariesDirectory, [string]$expertSourceDirectory){
        switch ($expertVersion){
            "7SP4" {
                # 
                MoveDeploymentFilesV7SP4 $binariesDirectory $expertSourceDirectory
            }
            "8" {
                # 
                MoveDeploymentFilesV8 $binariesDirectory $expertSourceDirectory
            }
            default {
                # Do nothing, all files are in Binaries folder. Applies to 7, 7SP1 and 7SP2.
            }
        }    
    }
    
    Function global:MoveDeploymentFilesV7SP4([string]$binariesDirectory, [string]$expertSourceDirectory){
        write "Moving Deployment files for V7.5"
        $deploymentDirectory = Join-Path $binariesDirectory 'DeploymentManager'
        
        CopySupportingFiles $deploymentDirectory $expertSourceDirectory 'deploymentManagerFilesList.txt'        
    }
    
    Function global:MoveDeploymentFilesV8([string]$binariesDirectory, [string]$expertSourceDirectory){
        write "Moving Deployment files for V8."
        $deploymentDirectory = Join-Path $binariesDirectory 'Deployment'
        CreateDirectory $deploymentDirectory        
        Start-Sleep -m 1500
        CopySupportingFiles $deploymentDirectory $expertSourceDirectory 'deploymentManagerFilesList.txt'
        
        #Move DeploymentManager
        write "Renaming DeploymentManager.exe to Setup.exe and moving to binaries directory."
        [void](MoveItem $(Join-Path $expertSourceDirectory 'DeploymentManager.exe') $(Join-Path $binariesDirectory 'Setup.exe'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'DeploymentManager.pdb') $(Join-Path $binariesDirectory 'Setup.pdb'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'DeploymentManager.exe.config') $(Join-Path $binariesDirectory 'Setup.exe.config'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'DeploymentManager.exe.log4net.xml') $(Join-Path $binariesDirectory 'Setup.exe.log4net.xml'))

        #Move DeploymentEngine
        write "Moving DeploymentEngine.exe to Deployment directory."
        [void](MoveItem $(Join-Path $expertSourceDirectory 'DeploymentEngine.exe') $(Join-Path $deploymentDirectory 'DeploymentEngine.exe'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'DeploymentEngine.exe.config') $(Join-Path $deploymentDirectory 'DeploymentEngine.exe.config'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'DeploymentEngine.exe.log4net.xml') $(Join-Path $deploymentDirectory 'DeploymentEngine.exe.log4net.xml'))
    }
    
    <#
    Finally we need to copy the license generator files depending on the version we are working on.  In theory there are three different scenarios:
    1. 7SP2 and earlier - all files are in Binaries folder. (Not yet immplemented)
    2. 7SP4 - all deployment files listed in ..\Build.Infrastructure\Src\Package\licenseGeneratorFilesList.txt are copied to Binaries\LicenseGenerator folder
       see GetProduct.ps1 (Function MoveLicenseGeneratorFiles) for details. (Not yet immplemented)
    3. 8 and later - all deployment files listed in ..\Build.Infrastructure\Src\Package\licenseGeneratorFilesList.txt are copied to Binaries\LicenseGenerator folder.
    #>     
    
    Function global:MoveInternalFiles([string]$expertVersion, [string]$expertSourceDirectory){
        switch ($expertVersion){
            "7SP4" {
                # Do nothing as Licensing is only immplemented in v8
                # MoveLicenseGeneratorFiles7SP4 $binariesDirectory $expertSourceDirectory
            }
            "8" {
                # 
                MoveInternalFilesV8 $expertSourceDirectory
            }
            default {
                # Do nothing, all files are in Binaries folder. Applies to 7, 7SP1 and 7SP2.
            }
        }    
    }
    
    Function global:MoveInternalFilesV8([string]$expertSourceDirectory){
        write "Moving License Generator files for V8."
        #Create 'Internal' folder under the source directory
        $internalDirectory = Join-Path $expertSourceDirectory 'Internal'
        CreateDirectory $internalDirectory        
        Start-Sleep -m 1500
        
        #Create 'LicenseGenerator' folder under the 'Internal' folder
        $licenseGeneratorDirectory = Join-Path $internalDirectory 'LicenseGenerator'
        CreateDirectory $licenseGeneratorDirectory
        $registrationServiceDirectory = Join-Path $internalDirectory 'RegistrationService'
        CreateDirectory $registrationServiceDirectory
        Start-Sleep -m 1500    
        
        CopySupportingFiles $licenseGeneratorDirectory $expertSourceDirectory 'licenseGeneratorFilesList.txt'
        
        #Move LicenseGenerator
        write "Moving LicenseGenerator.exe to .\Internal\LicenseGenerator under binaries directory."
        [void](MoveItem $(Join-Path $expertSourceDirectory 'LicenseGenerator.exe') $(Join-Path $licenseGeneratorDirectory 'LicenseGenerator.exe'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'LicenseGenerator.pdb') $(Join-Path $licenseGeneratorDirectory 'LicenseGenerator.pdb'))

        #Move PackagePackager
        write "Moving PackagePackager.exe to .\Internal\LicenseGenerator under binaries directory."
        [void](MoveItem $(Join-Path $expertSourceDirectory 'PackagePackager.exe') $(Join-Path $licenseGeneratorDirectory 'PackagePackager.exe')) 
        [void](MoveItem $(Join-Path $expertSourceDirectory 'PackagePackager.pdb') $(Join-Path $licenseGeneratorDirectory 'PackagePackager.pdb'))    
        
        #Move RegistrationService
        write "Moving Aderant.Registration.Service.zip to .\Internal\RegistrationService under binaries directory."
        [void](MoveItem $(Join-Path $expertSourceDirectory 'Aderant.Registration.Service.zip') $(Join-Path $registrationServiceDirectory 'Aderant.Registration.Service.zip'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'Aderant.Registration.Service.SourceManifest.xml') $(Join-Path $registrationServiceDirectory 'Aderant.Registration.Service.SourceManifest.xml'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'Aderant.Registration.Service.SetParameters.xml') $(Join-Path $registrationServiceDirectory 'Aderant.Registration.Service.SetParameters.xml'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'Aderant.Registration.Service.deploy-readme.txt') $(Join-Path $registrationServiceDirectory 'Aderant.Registration.Service.deploy-readme.txt'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'Aderant.Registration.Service.deploy.cmd') $(Join-Path $registrationServiceDirectory 'Aderant.Registration.Service.deploy.cmd'))
    }
    
    <#
    Below is the helper functions used for Move/Copy deployment and internal files
    #>
    
    Function global:CopySupportingFiles([string]$deploymentDirectory, [string]$expertSourceDirectory, [string]$fileListContainer) {
        #Copy all supporting files.
        write "Copying deployment dependencies to Deployment directory."
        $deploymentManagerFilesListPath = $fileListContainer
        #If global:PackageScriptsDirectory is defined use this path instead of the working directory, because the build servers do not use the Aderant PS profile.
        if ($global:PackageScriptsDirectory) {$deploymentManagerFilesListPath = Join-Path $global:PackageScriptsDirectory $fileListContainer}
        get-content -Path $deploymentManagerFilesListPath | Where-Object  {-not ($_.StartsWith("#"))} | ForEach-Object {CopyItem $expertSourceDirectory\$_ $deploymentDirectory\$_ -Force}    
    }
    
    Function global:MoveItem([string] $source, [string] $destination) {
        if(Test-Path $source){
           Move-Item -Path $source -Destination $destination -Force
        }
    }

    Function global:CopyItem([string] $source, [string] $destination) {
        if(Test-Path $source){
            #Check if destination folder exists
            $destinationFolder = Split-path -Path $destination -Parent
            if (-not(Test-Path $destinationFolder)){
                New-Item $destinationFolder -type directory
            }
           Copy-Item -Path $source -Destination $destination -Force
        }
    }

    Function global:CreateDirectory([string] $directoryPath) {
        if(!$(Test-Path($directoryPath))){
            New-Item -ItemType Directory -Path $directoryPath
        }
    }
    
    function global:RemoveEmptyFolders($folder) {      
        $items = Get-ChildItem $folder
        
        foreach($item in $items) {
            if ($item.PSIsContainer) {
                RemoveEmptyFolders $item.FullName
                
                $subitems = Get-ChildItem -Path $item.FullName
                if ($subitems -eq $null) {                    
                    Remove-Item $item.FullName -Force -ErrorAction SilentlyContinue
                }
                $subitems = $null
            }
        }
    }

    Function global:FormatCopyMessage($pipeline) {
        # Workaround for /NP not being compatible with /MT which fills up stdout with copy progress
        if ($pipeline -eq $null) {
            return
        }
        
        if ($pipeline.Contains("%")) {
            return
        }
        if ([String]::IsNullOrEmpty($pipeline)) {
            return
        }
        Write-Debug $pipeline.TrimStart().PadLeft(10)
    }

    Function global:GetBranchNameFromDropPath([string]$dropPath) {
        $parts = $dropPath.TrimEnd('\').Split('\')
        
        if ($parts -notcontains "dev" -and $parts -notcontains "releases" -and $parts -notcontains "main") {
            return $dropPath
        }
        
        return [string]$parts[$parts.Length-2] + "\" + $parts[$parts.Length-1]
    }
    
    Function global:WriteGetBinariesMessage([System.Xml.XmlNode]$module, [string]$dropPath) {
        $binariesText = $null
        if (IsThirdparty($module.Name) -and $module.Path -ne $null) {            
            $binariesText = "Getting third party binaries "            
        } else {
            $binariesText = "Getting binaries "
        }        
        
        Write-Host $binariesText -NoNewline -ForegroundColor Gray
        Write-Host $module.Name -NoNewline -ForegroundColor Green
        Write-Host " from the branch " -ForegroundColor Gray -NoNewline
        if ([string]::IsNullOrEmpty($module.Action) -and [string]::IsNullOrEmpty($module.Path)) {                
            Write-Host (GetBranchNameFromDropPath $dropPath) -ForegroundColor Green
        } else {
            Write-Host $module.Path -ForegroundColor Green
        }        
    }    
    