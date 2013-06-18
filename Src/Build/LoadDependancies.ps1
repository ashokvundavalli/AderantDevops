<# 
.Synopsis 
    Co-ordinates logic to pull down all dependancies for this module from the drop server
.Example         
    LoadDependancies -$modulesRootPath \\na.aderant.com\ExpertSuite\Dev\<Branch>     
.Parameter $modulesRootPath is the path to the root where the modules are found
.Parameter $buildScriptsDirectory is an optional parameter is used by the build servers
#> 
param([string]$modulesRootPath, [string]$dropPath, [string]$buildScriptsDirectory)

begin {    
    $jobList = @()
    
    ###
    # Get the common Build-Libraries
    ###
    Function LoadLibraries([string]$fromBuildScriptsDirectory, [string]$to){            
        $fromBuildScriptsDirectory = (Join-Path $fromBuildScriptsDirectory  \Build-Libraries.ps1)                    
        Copy-Item -Path $fromBuildScriptsDirectory -Destination $to -Force            
        $shell = (Join-Path $to  Build-Libraries.ps1)             
        &($shell)                                           
    }
    
    # Copies a list of given files to a series of specific Web module folders
    function ProcessWebDependencies($dependencyDirectory, $projectFolders, $module, $files) {   
        foreach ($folder in $projectFolders) {
            Write-Debug "Processing folder $folder" 

            $fn = $folder.Name
        
            $scriptsFolder = "$modulesRootPath\src\$fn\Scripts"
            CopyFilesToFolder $dependencyDirectory $files "$scriptsFolder" $module.Name @("*.js")
        
            $contentFolder = "$modulesRootPath\src\$fn\Content\Includes"
            CopyFilesToFolder $dependencyDirectory $files "$contentFolder" $module.Name @("*.css", "*.less", "*.png")
        }
    }
    
    # Copies a list a list of files to a folder
    function CopyFilesToFolder($dependencyDirectory, $localFiles, $destination, $moduleFolderName, $include) {
        if (Test-Path "$destination\$moduleFolderName") {
            Write-Debug "Removing $destination\$moduleFolderName"
            Remove-Item -Recurse -Force "$destination\$moduleFolderName"
        }
        
        if (!(Test-Path "$destination\$moduleFolderName")) {
            New-Item -Path "$destination\$moduleFolderName" -Type Directory | Out-Null
        }            
        
        foreach ($file in $localFiles) {
            $source = $file
            if ($file -is [System.IO.FileInfo]) {
                $source = $file.FullName
                                
                $directory = $file.DirectoryName
                $relativedestination = (ReplaceText $directory $dependencyDirectory "").Trim('\')
                if (-not [string]::IsNullOrEmpty($relativedestination)) {                                                        
                    $destinationFolder = [System.IO.Path]::Combine("$destination\$moduleFolderName", $relativedestination)                        
                    New-Item -Path $destinationFolder -Type Directory -ErrorAction SilentlyContinue | Out-Null
                }
                
                $sourceFileName = [System.IO.Path]::GetFileName($file)
                $destinationFilePath = [System.IO.Path]::Combine("$destination\$moduleFolderName", $relativedestination, $sourceFileName)
                
                Copy-Item $source $destinationFilePath -Include $include -Recurse -Force | Out-Null
            }
        }
    }
    
    function ReplaceText($str, $find, $replacement) {
        $i = $str.IndexOf($find, [System.StringComparison]::OrdinalIgnoreCase)
        $len = $find.Length;
        return $str.Replace($str.Substring($i, $len), $replacement)        
    }
}

process {
    # Canonicalize the path - fix doubled up slashes etc
    $dropPath = [System.IO.Path]::GetFullPath($dropPath)

    [string]$profileDevelopment = $env:ExpertProfileDevelopment
    if (-not [string]::IsNullOrEmpty($profileDevelopment)) {
        Write-Debug "Using Aderant Profile Development mode"
        $buildScriptsDirectory = Join-Path $env:ExpertDevBranchFolder -ChildPath "Modules\Build.Infrastructure\Src\Build"
    }    
        
    [string]$moduleBuildDirectory = (Join-Path $modulesRootPath  \Build)
    [string]$moduleCommonBuildDirectory = (Join-Path $modulesRootPath  \CommonBuild)
    
    if (!(Test-Path $moduleCommonBuildDirectory)){ 
        New-Item -Path $moduleCommonBuildDirectory -ItemType directory 
    }
    
    [string]$moduleDependenciesDirectory = (Join-Path $modulesRootPath  \Dependencies)                  
        
    if ([string]::IsNullOrEmpty($buildScriptsDirectory)){                  
        $buildScriptsDirectory = Join-Path -Path $dropPath -ChildPath "\Build.Infrastructure\Src\Build"                                      
        LoadLibraries $buildScriptsDirectory $moduleCommonBuildDirectory
    } else {
        LoadLibraries $buildScriptsDirectory $moduleCommonBuildDirectory 
    }

    Write-Debug "Using $buildScriptsDirectory as build script directory"
                                         
    DeleteContentsFrom $moduleDependenciesDirectory                                            
    
    [xml]$expertmanifest = LoadExpertManifest $buildScriptsDirectory
    [xml]$manifest = LoadManifest $moduleBuildDirectory
        
    if([string]::IsNullOrEmpty($manifest.DependencyManifest.ReferencedModules)){
        Write-Debug "There are no referenced modules"
    } else {
       $startTime = Get-Date
        
        $modules = $manifest.DependencyManifest.ReferencedModules.SelectNodes("ReferencedModule") 
        Write-Debug "There are $($modules.Count) referenced modules"   
        
        $moduleName = [System.IO.Path]::GetFileName($modulesRootPath)        
        $folders = Get-ChildItem $modulesRootPath\src\ | ?{ $_.PSIsContainer }
           
        foreach ($module in $modules) {   
            [System.Xml.XmlNode]$expertManifestModuleDef = FindModuleFromManifest $expertManifest.ProductManifest.Modules $module.Name
            
            if ($expertManifestModuleDef -eq $null) {                
                throw [string]"Could not locate an entry for $($module.Name) in the Expert Manifest. Update the Expert Manifest to include this module and a branch location and try again."                                 
            }            
            
            if ($expertManifestModuleDef) {
                Write-Debug "Using Expert Manifest dependency definition for $($module.Name)"
                $module = $expertManifestModuleDef
            }
                                   
            [string]$moduleBinariesPath = GetPathToBinaries $module $dropPath
              
            if (-not (Test-Path $moduleBinariesPath)) {
                throw [string]"The drop location `"$moduleBinariesPath`" for the module `"$($module.Name)`" does not exist"
            }

            WriteGetBinariesMessage $module $dropPath
                                 
            if (([string]$moduleBinariesPath).Contains("Web."))  {
                # web files are packaged using webdeploy, and need to be unzipped and copied in a specific way               
                Write-Host "    Unzipping web module ..." -ForegroundColor Gray
                $jobList += Start-Job -ScriptBlock {  
                    Param ($buildScriptsDirectory, $moduleBinariesPath, $moduleDependenciesDirectory)
                    # Pull down and extract the zipped Web.* module                
                    Invoke-Expression "$buildScriptsDirectory\..\build.tools\GetWebProjectDependencies.exe $moduleBinariesPath $moduleDependenciesDirectory"
                } -ArgumentList $buildScriptsDirectory, $moduleBinariesPath, $moduleDependenciesDirectory
            } else {
                CopyContents $moduleBinariesPath $moduleDependenciesDirectory
                                    
                # If we are getting a third party module and we are a Web module
                # then our dependencies need to go into additional directories
                if ((IsThirdParty $module) -and ($moduleName -like "Web.*")) {
                    Write-Host -ForegroundColor Gray "   Copying web dependencies"
                    $contents = Get-ChildItem -Recurse $moduleBinariesPath
                    
                    $localCopyOfFiles = @()
                    # For the contents of the remote module create an location array of the local copy
                    foreach ($file in $contents) {
                        # Replace just the path to preserve the file name casing
                        $path = [System.IO.Path]::GetDirectoryName($file.FullName)                        
                        $localPath = ReplaceText $path $moduleBinariesPath $moduleDependenciesDirectory                        
                        $localPath = [System.IO.Path]::Combine($localPath, $file.Name)
                        
                        if ($file -is [System.IO.DirectoryInfo]) {
                            $localCopyOfFiles += New-Object System.IO.DirectoryInfo($localPath)                                
                        } else {
                            $localCopyOfFiles += New-Object System.IO.FileInfo($localPath)
                        }
                    }                    
                    
                    # Iterate all projects in the src folder, copying in js and css dependencies if they exist
                    ProcessWebDependencies $moduleDependenciesDirectory $folders $module $localCopyOfFiles 
                }
            }
        }      
        
        Write-Host -ForegroundColor Gray "Waiting for background tasks ..."
        $jobList | Wait-Job | Receive-Job
        Write-Host -ForegroundColor Gray "Done"
        
        if ($moduleName -like "Web.*") {        
            Write-Host "    Synchronizing projects ..." -ForegroundColor Gray            
            Invoke-Expression "$buildScriptsDirectory\..\build.tools\WebDependencyCsprojSynchronize.exe $moduleDependenciesDirectory"             
            
            foreach ($folder in $folders) {
                RemoveEmptyFolders $folder.FullName
            }
        }        
    }
    
    $time = [string]::Format("Dependencies copied in {0:mm}:{0:ss}.{0:ff}", ($($(Get-Date) - $startTime)))
    Write-Host $time     
}
