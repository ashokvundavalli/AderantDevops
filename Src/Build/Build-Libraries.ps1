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
    
        if((IsThirdparty $module) -or (IsHelp $module)){
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
            $start = $dropPath.Substring(0,$dropPath.LastIndexOf("release\"))
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
        
        if($action.Equals("other-branch-external-module") -and ![string]::IsNullOrEmpty($module.Path)){  
            Write-Host "Getting third party binaries for " $module.Name  " from the branch " $module.Path      
            $rootPath = ChangeBranch $rootPath $module.Path                    
        } 
             
        return (Join-Path $rootPath  ($module.Name+'\Bin'))
    }                                                           
    
    ##
    # Versioned binaries path from the drop
    ##
    Function global:ServerPathToModuleBinariesFor([System.Xml.XmlNode]$module, [string]$dropPath, [string]$action="current-branch"){                                            
        if(!$dropPath){$rootPath=(Get-DropRootPath)}else{$rootPath=$dropPath}         
                
        if($action.Equals("other-branch") -and ![string]::IsNullOrEmpty($module.Path)){  
            Write-Host "Getting binaries for "  $module.Name " from the branch " $module.Path          
            $rootPath = ChangeBranch $rootPath $module.Path                    
        }                                                       
        
        $binModule = '\Bin\Module'
        
        $pathToModuleAssemblyVersion = Join-Path -Path (Join-Path $rootPath $module.Name) -ChildPath $module.AssemblyVersion 
        
        if($module.HasAttribute("FileVersion")){
            $modulePath = Join-Path -Path( Join-Path -Path $pathToModuleAssemblyVersion  -ChildPath $module.FileVersion) -ChildPath $binModule
        }else{                                
            $modulePath = PathToLatestSuccessfulBuild $pathToModuleAssemblyVersion
        }
               
        return  $modulePath                                                
    }
    
    ##
    # Versioned test binaries path from the drop
    ##
    Function global:ServerPathToModuleTestBinariesFor([System.Xml.XmlNode]$module, [string]$dropPath){                                            
        if(!$dropPath){$rootPath=(Get-DropRootPath)}else{$rootPath=$dropPath}         
        
        $binTest = '\Bin\Test'
        
        $pathToModuleAssemblyVersion = Join-Path -Path (Join-Path -Path $rootPath -ChildPath $module.Name) -ChildPath $module.AssemblyVersion 
        
        if($module.HasAttribute("FileVersion")){
            $testBinPath = Join-Path -Path( Join-Path -Path $pathToModuleAssemblyVersion  -ChildPath $module.FileVersion) -ChildPath $binTest
        }else{                                            
            $testBinPath = PathToLatestSuccessfulBuild $pathToModuleAssemblyVersion
        }               
        return  $testBinPath        
    }          
        
    ###
    # Find the last successfully build in the drop location.
    ###
    Function global:PathToLatestSuccessfulBuild([string]$pathToModuleAssemblyVersion){
        $sortedFolders = SortedFolders $pathToModuleAssemblyVersion
        [bool]$noBuildFound = $true
        [string]$pathToLatestSuccessfulBuild
        foreach ($folderName in $sortedFolders ) {            
        
            $buildLog = Join-Path -Path( Join-Path -Path $pathToModuleAssemblyVersion -ChildPath $folderName.Name ) -ChildPath "\BuildLog.txt"
            $pathToLatestSuccessfulBuild = Join-Path -Path( Join-Path -Path $pathToModuleAssemblyVersion -ChildPath $folderName.Name ) -ChildPath "\Bin\Module"
            
            [string]$buildFailed                        
            if(Test-Path $buildLog){
                $buildFailed = Get-Content -Path $buildLog | where {$_.Contains("Build FAILED")} | Out-Null            
            }
            
            if ([string]::IsNullOrEmpty($buildFailed) -and (test-path $pathToLatestSuccessfulBuild)) {                                            
                return $pathToLatestSuccessfulBuild
            }
        }     
        
        if($noBuildFound){
            throw "no latest build found for ["+$pathToModuleAssemblyVersion+"]"
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