<# 
.Synopsis 
    Functions relating to be modules 
.Example     
        
.Remarks
#>    
    
    ###
    # Loads the local dependency manifest
    ###
    Function global:LoadManifest([string]$manifestPath){                
        return Get-Content ($manifestPath + "\DependencyManifest.xml") -Force
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
        
    ##
    # Resolves the path to the binaries for the given module
    ##
    Function global:PathToBinaries([System.Xml.XmlNode]$module, [string]$dropPath){
        $action = FindGetActionTag $module            
        Switch ($action)
        {
          "local"  { LocalBinariesPathFor $module }
          "thirdparty"  { ThirdpartyBinariesPathFor  $module $dropPath}          
          "branch"  { BranchBinariesPathFor  $module $dropPath}
          "serverdrop"  { ServerBinariesPathFor $module $dropPath}
          Default { throw "invalid action [$action]"}
        }
    }
    
    
    ##
    # Resolves the path to the binaries for the given module
    ##
    Function global:PathToLocalBinaries([System.Xml.XmlNode]$module, [string]$dropPath){
    
        [string]$path        
        if(IsThirdparty $module){
            $path = ThirdpartyBinariesPathFor $module $dropPath
        }else{
            $path = LocalBinariesPathFor $module 
        }
        return $path
    }        
    
    ##
    # Find the get action for this module
    ##
    Function global:FindGetActionTag([System.Xml.XmlNode]$module){
    
        if($module.HasAttribute("GetAction") -and ([string]::IsNullOrEmpty($module.GetAction) -ne $true)){
            return $module.GetAction
        }elseif(IsThirdparty $module){   
            return $action="thirdparty"            
        }
        return "serverdrop"                         
    }
        
    ##
    # Change and Test the drop path to a new branch
    ##
    Function global:ChangeBranch([string]$dropPath, [string]$branchName){                 
        $start = $dropPath.Substring(0,$dropPath.LastIndexOf('\'))    
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
    Function global:IsThirdparty([System.Xml.XmlNode]$module){
        return $module.Name.Split(".",2)[0].Equals("thirdparty","InvariantCultureIgnoreCase")
    }
    
    ###
    # Is this the help module?
    ###
    Function global:IsHelp([System.Xml.XmlNode]$module){
        return $module.Name.Equals("expert.help","InvariantCultureIgnoreCase")
    }                                   
        
    ##
    # Local binaries path
    ##
    Function global:LocalBinariesPathFor([System.Xml.XmlNode]$module){
    
        #in assests dir
        $moduleLocalPath
        if($module.HasAttribute("Path")){
            $moduleLocalPath = (Join-Path $module.Path  ($module.Name + '\Bin\Module'))
        }else{
            #Get-LocalModulesRootPath
            $moduleLocalPath = Resolve-Path ('.\..\..\..\'+ $module.Name + '\Bin\Module')
        }    
        return $moduleLocalPath
    }
    
    ##
    # Thirdparty binaries path from the drop
    ##
    Function global:ThirdpartyBinariesPathFor([System.Xml.XmlNode]$module, [string]$dropPath){                                                   
        if(!$dropPath){$rootPath=(Get-DropRootPath)}else{$rootPath=$dropPath}
        
        return (Join-Path $rootPath  ($module.Name+'\Bin'))
    }
    
    ##
    # Versioned binaries path from the drop
    ##
    Function global:ServerBinariesPathFor([System.Xml.XmlNode]$module, [string]$dropPath){                                            
        if(!$dropPath){$rootPath=(Get-DropRootPath)}else{$rootPath=$dropPath}         
        $moduleAssemblyVersionPath = $rootPath + '\' + $module.Name + '\' + $module.AssemblyVersion + '\'                        
        $latestVersionFolderName = FindLatestSuccessfulBuildFolderName $moduleAssemblyVersionPath
               
        return  (Join-Path $moduleAssemblyVersionPath  ($latestVersionFolderName+'\Bin\Module'))                                                
    }
    
    ##
    # Versioned test binaries path from the drop
    ##
    Function global:ServerTestBinariesPathFor([System.Xml.XmlNode]$module, [string]$dropPath){                                            
        if(!$dropPath){$rootPath=(Get-DropRootPath)}else{$rootPath=$dropPath}         
        $moduleAssemblyVersionPath = $rootPath + '\' + $module.Name + '\' + $module.AssemblyVersion + '\'                        
        $latestVersionFolderName = FindLatestSuccessfulBuildFolderName $moduleAssemblyVersionPath
               
        return  (Join-Path $moduleAssemblyVersionPath  ($latestVersionFolderName+'\Bin\Test'))                                                
    }
    
    ##
    # Versioned binaries path from the drop
    ##
    Function global:BranchBinariesPathFor([System.Xml.XmlNode]$module, [string]$dropPath){                                            
        if(!$dropPath){$rootPath=(Get-DropRootPath)}else{$rootPath=$dropPath}
        
        if(!$module.HasAttribute("Path")){ 
            throw "the module ["+$module.Name+"] needs to specify the branch in the Path attribute"
        }
        
        $rootPath = ChangeBranch $rootPath $module.Path
                 
        $moduleAssemblyVersionPath = $rootPath + '\' + $module.Name + '\' + $module.AssemblyVersion + '\'                        
        $latestVersionFolderName = FindLatestSuccessfulBuildFolderName $moduleAssemblyVersionPath
               
        return  (Join-Path $moduleAssemblyVersionPath  ($latestVersionFolderName+'\Bin\Module'))                                                
    }
    
        
    ###
    # Find the last successfully build in the drop location.
    ###
    Function global:FindLatestSuccessfulBuildFolderName([string]$startOfDropLocation){
        $sortedFolders = SortedFolders $startOfDropLocation
        foreach ($folderName in $sortedFolders ) {
            $buildLog = $startOfDropLocation + $folderName.Name + '\' + "BuildLog.txt"
            if (test-path $buildLog) {
                if (Get-Content $buildLog | select -last 3 | where {$_.Contains("0 Error(s)")}) {
                    $latestVersionFolderName = $folderName;
                    break;
                }
            }
        }     
        return $latestVersionFolderName.Name
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
                                           
       Robocopy.exe $copyFrom.Trim() $copyTo.Trim() /E /NP /XX /NJS /NJH            
    }                
    
    ##
    # 
    ##
    Function global:CopyModuleBinariesDirectory([string]$from, [string]$to,[bool]$includePdbFiles){
        write "Copying $from to $to, include pdbs? [$includePdbFiles]"
        if($includePdbFiles){
            Copy-Item $from\* -Destination $to -Recurse -Force
        }else{    
            Copy-Item $from\* -Destination $to -Recurse -Force -Exclude *.pdb 
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
           New-Item -ItemType Directory -Path $dropBinModulePath
       }
              
       ResolveAndCopyUniqueBinModuleContent -modulePath $modulePath -copyToDirectory $dropBinModulePath                                                                                                          
                     
       #Test folder that we don't want to strip dependencies from 
       if(Test-Path $binTestPath){       
            if(!(Test-Path $dropBinTestPath)){
                New-Item -ItemType Directory -Path $dropBinTestPath
            }                    
            CopyContents -copyFrom $binTestPath -copyTo $dropBinTestPath 
       }
    }            
    
    <#
    Copies only built files, i.e. excludes items that are dependancies, from Bin\Module
    #>
    Function global:ResolveAndCopyUniqueBinModuleContent([string]$modulePath, [string]$copyToDirectory){                
       
       $dependenciesPath = Join-Path $modulePath Dependencies
       $binPath = Join-Path $modulePath Bin\Module
              
       [Array]$uniqueItems += dir -Path $binPath -Recurse -Exclude( dir $dependenciesPath | ForEach-Object {$_.Name}) | 
       Where-Object {$_.PSIsContainer -ne $true} | 
       ForEach-Object {$_.FullName}
              
       [string]$binModulePart = "Bin\Module\" 
           
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
                   
            Robocopy.exe $fromPath $toPath $fileName /NP /XX /NJS /NJH                
       }
    }
    
    <#
    Builds Query Sercice with local with specified models.
    params:
        zipFile - full path of source zip, empty string for in place build.
        dmdslPaths - comma seperated list of paths to search for dmdsl files.
        outPath - where to copy build output to
    #>
    Function global:BuildQueryService([string]$ServicesQueryModule, $dmdslPaths, $outPaths, [string]$buildModulePath){
    
        write "**********************************************************************"
        write "** Building Query Service"
        write "** ServicesQueryModule = $ServicesQueryModule"
        write "** dmdslPaths = $dmdslPaths"
        write "** outPaths = $outPaths"
        write "** buildModulePath = $buildModulePath"
        write "**********************************************************************"
        write "** NB: THIS SCRIPT REQUIRES:"
        write "**          - Source for Service.Query module, with its dependencies."
        write "**          - Source for Build.Infrastructure."
        write "**********************************************************************"        

        $queryServiceDependenciesFolder = ($ServicesQueryModule + "\Dependencies")
        
        #Delete all dmdsl files from Services.Query\Dependencies
        get-childitem  -Path $queryServiceDependenciesFolder -Filter *.dmdsl -Recurse | Remove-Item -Path {$_.FullName} -Force
        
        #gather all dmdsls recurively into Services.Query/dependencies.
        foreach( $dmdslPath in $dmdslPaths){
            $dmdsls = get-childitem  -Path $dmdslPath -Filter *.dmdsl -Recurse 
            if ($dmdsls){
                foreach($dmdsl in $dmdsls){
                    copy-item -Path $dmdsl.FullName -destination $queryServiceDependenciesFolder -Force 
                    write "** Getting $dmdsl"
                }
            }
        }
        
        #build Services.Query
        write "** Compiling Query Service..."
        if ($outpaths.Count){
            $buildlog = Join-Path $outPaths[0] buildlog.txt
        } else {
            $buildlog = Join-Path $outPaths buildlog.txt
        }
        
        $shell = $buildModulePath + "\BuildModule.ps1 -moduleToBuildPath " + $ServicesQueryModule 
        invoke-expression -Command $shell  | Out-File $buildlog
        if(CheckBuild $buildlog){         
            #copy output to outPaths
            write "** Query Service Build Succeeded!"
            write "**********************************************************************"   
            foreach( $outPath in $outPaths){
                CopyContents ($ServicesQueryModule + "\Bin\Module") $outPath
            }
        }else{
           $buildError = "Build Failed!! Check the log " + $buildLog                                 
           Write-Error -Message $buildError -ErrorAction Stop
        }
    }
    
    
    <#
    Checks tail of a build log.  If build successful returns True.
    #>    
    Function global:CheckBuild([string]$buildLog){
                    
        $noErrors = Get-Content $buildLog | select -last 10 | where {$_.Contains("0 Error(s)")}
                    
        if ($noErrors){      
           return $true           
        }else{ 
           return $false
        }
    }    
    
    <#
    Gets model files
    #>
    Function global:GetModelsForProduct([string]$productManifestPath, [string]$dropPath, [string]$target){
        write "Function global:GetModels"        
        [xml]$product =  Get-Content $productManifestPath
        foreach($module in $product.ProductManifest.Modules.SelectNodes("Module")){        
            
            if((IsThirdparty $module) -or (IsHelp $module)){            
                #Do nothing                    
            }else{
                #Search here for models.

                $moduleBinariesDirectory = ServerBinariesPathFor $module $dropPath 
                $dmdsls = get-childitem  -Path $moduleBinariesDirectory -Filter *.dmdsl -Recurse 
                if ($dmdsls){
                    foreach($dmdsl in $dmdsls){
                        $message = "Getting " + $module.Name + " - $dmdsl"
                        write $message
                        copy-item -Path $dmdsl.FullName -destination $target -Force 
                    }
                }
                
            }                                               
                         
        }        
    }