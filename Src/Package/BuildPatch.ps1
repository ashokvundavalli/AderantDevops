<#  
.Synopsis 
    Represents a mechanism to process a collection of ChangeSetInfo to create patch package.
.Parameter expertBinariesDirectory
    The location of the the binaries to construct a patch from.
.Parameter outputDirectory
    The location the patch should be copied to.
.Parameter packageScriptsDirectory
    The location of PatchBuilder.dll
.Example
    pushd $PackageScriptsDirectory; .\GetPatchChangeSets.ps1 -teamFoundationServer http://tfs.ap.aderant.com:8080/ -teamProject 'ExpertSuite' | .\BuildPatch.ps1 -packageScriptsDirectory $PackageScriptsDirectory -expertBinariesDirectory $BranchBinariesDirectory -outputDirectory $BranchBinariesDirectory
#>
param(
    [string]$packageScriptsDirectory = "",
    [string]$expertBinariesDirectory = "C:\ExpertShare",
    [string]$outputDirectory = "C:\ExpertShare"
)

$ErrorActionPreference = "Stop"
$packagePlugins = @()
$databaseObjects = @()

# Helper to workout if this file is a database object
function IsDatabaseObject($file) {
    return ($file.StartsWith("$") -and $file.ToLower().EndsWith(".sql"))
}

function AddPackageFile($package, [System.IO.FileInfo]$file) { 
    $files = New-Object System.Collections.ObjectModel.Collection[Aderant.Framework.Packaging.Process.IncludeFile]
   
    if ($file.Exists) {
        $path = ($expertBinariesDirectory + "`\").Replace("\", "/")
        $f = ($file.FullName).Replace("\", "/") -replace $path, [System.String]::Empty
        $relativeFilePath = [System.IO.Path]::GetDirectoryName($f)       
             
        $includeFile = New-Object Aderant.Framework.Packaging.Process.IncludeFile($file, $relativeFilePath, $false)
        $files.Add($includeFile)
       
        $package.AddFiles($files)
    } else {
        $source = [System.IO.Path]::GetDirectoryName($file)
        Write-Host "Skipping $file as it does not exist. It will not be added to the package."
    }
}


function CreatePackage() {
	[xml]$manifest = Get-Content .\PatchingManifest.xml
	$name = $manifest.Settings.PatchName

    [void][System.Reflection.Assembly]::LoadFrom([System.IO.Path]::Combine($expertBinariesDirectory, "Aderant.Framework.Packaging.Dtos.dll"))
    
    $definition = New-Object Aderant.Framework.Packaging.Dtos.PackageDefinitionDto
    $definition.Id = New-Object Aderant.Framework.Identifier([System.Guid]::NewGuid())
    
    Write-Host "Creating new package: $($definition.Id)."
        
    $definition.OwnerId = [GUID]("00000000-0000-0000-0000-00000000000A")
    $definition.Name = $name
        
    $package = New-Object Aderant.Framework.Packaging.Process.Package       
    $package.Definition = $definition
    $package.Owner.Name = "Aderant"     
    $package.Owner.Id = New-Object Aderant.Framework.Identifier("00000000-0000-0000-0000-00000000000A")
    
    Write-Host "Created package $($package.Name)."
    
    return $package
}


function ExportPackage($packager, $package, $projects) {
    Write-Host "Exporting package: $($package.Definition.Name)."
    
    Write-Host "Adding plugins to package"
    
    $versionControl = [Aderant.BuildTools.PatchBuilder.ProjectFactory]::VersionControl
    if ($versionControl -eq $null) {
        throw (New-Object [System.ArgumentNullException])
    }
    
    # Handle plugins
    foreach ($plugin in $packagePlugins) {
        $plugin.SetDefinition($package.Definition)
        $plugin.SetPackageOwner($package.Owner)
        
        $pluginDirectory = $packager.CreatePluginPackageDirectory($plugin.GetPackageDirectory())
        
        Write-Host "Created plugin directory $pluginDirectory"
        
        foreach ($project in $projects) {
            $pluginItems = $project.GetPackagingArtifacts($plugin.GetCode())
            
            foreach ($pluginItem in $pluginItems) {
                $localFile = [System.IO.Path]::GetFileName($pluginItem.File)
                $localFile = [System.IO.Path]::Combine($pluginDirectory, $localFile)
            
                $versionControl.DownloadFile($pluginItem.File, $localFile)                
            }
        }
        $packager.RemoveEmptyPluginPackageDirectory($plugin.GetPackageDirectory())
    }
    
    # Add all plugin files to package
    $package.Add($packager.PackagingDirectory)
    
    # Handle BinFiles
    $files = @()    
    
    foreach ($project in $projects) {    
        if (![String]::IsNullOrEmpty($project.AssemblyFileName)) {
            $foundFiles = FindFile $project.AssemblyFileName
            if ($foundFiles -ne $null) {
                foreach ($file in $foundFiles) {
                    # Until Packaging can handle duplicate destinations (line 185 in Packager.cs) ignore DM files
                    if (!$file.FullName.Contains("DeploymentManager")) {            
                        $files += $file
                    }
                }
            } else {
                Write-Warning "Could not locate $file"
            }                
        }
        
        foreach ($artifact in $project.Artifacts) {
            if (IsDatabaseObject($artifact)) {
                $script:databaseObjects += $artifact
            } else {
                $foundFiles = FindFile $artifact
                
                if ($foundFiles -ne $null) {
                    foreach ($file in $foundFiles) {
                        # Until Packaging can handle duplicate destinations (line 185 in Packager.cs) ignore DM files
                        if (!$file.FullName.Contains("DeploymentManager") -or ($file.FullName.EndsWith(".xml"))) {            
                            $files += $file
                        }
                    }
                } else {
                    Write-Warning "Could not locate $file"
                }
            }
        }
    }
    
    $files | Sort-Object -Unique | ForEach-Object { AddPackageFile $package $_ }    
    
    $outzip = $null

    try {
        # "save" = false so we do not call the service
        $ret = $packager.ExportPackage($package, $false)

        try {
            $dir = [System.IO.Path]::Combine($outputDirectory, "PatchOutput")
            
            # Clean the directory
            Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
            
            Write-Host "Creating patch output directory: $dir"
            [Void][System.IO.Directory]::CreateDirectory($dir) 
            
			Write-Host "Copying patch zip to drop location."
            Copy-Item $ret $dir
            $outzip = [System.IO.Path]::Combine($dir, [System.IO.Path]::GetFileName($ret))
        } catch [System.Exception] {
            Write-Error "Error while creating patch drop directory $dir and copying zip $($_.Exception.Message)."
            throw
        }
    } catch [System.Exception] {
        Write-Error "Error while attempting to create patch. Process aborted. $($_.Exception.Message)."
        
        if ($($_.Exception.InnerException -ne $null)) {
            Write-Error "Error while attempting to create patch. $($_.Exception.InnerException.Message)."
        }
        throw
    } finally {
        Write-Host "Cleaning up temporary directory."
        if ([System.IO.Directory]::Exists($packager.OutputDirectory)) {
            [System.IO.Directory]::Delete($packager.OutputDirectory, $true)
        }
        
        Write-Host "Attempting cleaning up of package directory."
        $packager.RemovePackagingDirectory()      
    }
	return $outzip
}


function FindFile([string]$file) {
    Write-Host "Searching for $file" -ForegroundColor "magenta"
    
    $fullFilePath = $null
    
    $fullFilePath = Get-ChildItem $expertBinariesDirectory -Recurse -Include $file
    
    if ($fullFilePath -eq $null) {    
        # handle the case where we might have a partial path
        $fullPath = [System.IO.Path]::Combine($expertBinariesDirectory, $file)
        
        $directory = [System.IO.Path]::GetDirectoryName($fullPath)
        $file = [System.IO.Path]::GetFileName($fullPath)
        
        $fullFilePath = Get-ChildItem $directory -Recurse -Include $file
    }
    
    if ($fullFilePath -eq $null) {
        Write-Warning "Could not locate $file"
    } else {
        foreach ($location in $fullFilePath) {
            Write-Host "Found $file in $($location.DirectoryName)" -ForegroundColor "DarkCyan"    
        }
    }
    
    return $fullFilePath    
}


function LoadPackagingAssemblies() {
    # Load the packaging assemblies to build the zip for us
    try {
        Write-Host "Loading packaging assemblies from $expertBinariesDirectory."
        [void][System.Reflection.Assembly]::LoadFrom([System.IO.Path]::Combine($expertBinariesDirectory, "Aderant.Framework.dll"))
        [void][System.Reflection.Assembly]::LoadFrom([System.IO.Path]::Combine($expertBinariesDirectory, "Aderant.Framework.Packaging.Process.dll"))
        [void][System.Reflection.Assembly]::LoadFrom([System.IO.Path]::Combine($expertBinariesDirectory, "Aderant.Framework.Packaging.Client.dll"))
        
        $script:packagePlugins = LoadPackagingPlugins $expertBinariesDirectory
    } catch [System.Exception] {
        if ($_.Exception.InnerException -ne $null) {
            Write-Error "Error: $($_.Exception.InnerException.Message)."
        }        
        Write-Error "Error while loading packaging assemblies in build output. Error: $($_.Exception.Message)."
        throw
    }
}


function LoadPackagingPlugins($binaries) {
    $pluginAssembly = [System.IO.Path]::Combine($binaries, "Aderant.Framework.PackagingPlugins.dll")

    $pluginList = @()
    
    if ([System.IO.File]::Exists($pluginAssembly)) {
        Write-Host "Loading packaging plugin assemblies from $binaries."
        [void][System.Reflection.Assembly]::LoadFrom($pluginAssembly)
        
        $assembly = [System.Reflection.Assembly]::LoadFrom($pluginAssembly)
        
        foreach ($type in $assembly.GetExportedTypes()) {
            if ($type.IsClass -and !$type.IsAbstract) {                    
                $plugin = [System.Runtime.Serialization.FormatterServices]::GetUninitializedObject($type.Fullname) -as [Aderant.Framework.Packaging.Process.Plugins.IPackagePlugin]
                
                if ($plugin -ne $null) {                
                    $pluginList += $plugin
                }
            }
        }        
    }
    return $pluginList
}


<# 
.Synopsis 
    Transforms a list of change sets into a packaged zip.
.Description
.Parameter changes
    The collection of ChangeSetInfo to build the patch for.
#>
function CreatePatch($changes) {
    if ($changes -eq $null -or $changes.Count -eq 0) {
        Write-Host "No changes found. Patch creation skipped."
        return
    }
	
    [void][System.Reflection.Assembly]::LoadFrom([System.IO.Path]::Combine($packageScriptsDirectory, "PatchBuilder.dll"))
    
    # Get a list of changes
    Write-Host "Getting changed project map for patch."
    $mappedChanges = [Aderant.BuildTools.PatchBuilder.PatchBuilder]::GetChangedProjectMap($changes)

    Write-Host "Fetched $($mappedChanges.Count) project change sets."
	
	LoadPackagingAssemblies
	
    $package = CreatePackage
    
    # Create a Packager
    # Invoke the logic manually to export the package to bypass any dependencies on an environment	
    $temporaryPatchDirectory = [System.IO.Path]::Combine($env:TEMP, "Patch-" + $package.Definition.Id)	
	$packager = New-Object Aderant.Framework.Packaging.Process.Packager($temporaryPatchDirectory, [string]::Empty)
    Write-Host "Packaging temporary directory: $($packager.OutputDirectory)."
        
	# structure 
    # Temp
    #     |- Patch-<guid>   <- # zip output goes here
    #         |-<guid>
    #             |-Contents 
    $packager.PackagingDirectory = [System.IO.Path]::Combine($packager.OutputDirectory, [System.Guid]::NewGuid())
    
    $projects = @()
    foreach ($change in $mappedChanges) {
        Write-Host "Changeset $($change.Id) contains $($change.Projects.Count) affected projects."        
        $change.Projects | ForEach-Object { $projects += $_ }
    }
    
    # Projects might be referenced by many change sets, build a unique list of projects for the current changes
    $projects = $projects | Sort-Object -Property ProjectFile -Unique
	    
    $patchLocation = ExportPackage $packager $package $projects
	
    if ($databaseObjects.Count -gt 0) {
        $dir = [System.IO.Path]::GetDirectoryName($patchLocation)
            Write-Host "Creating database update"
            .\CreateDatabaseUpdate.ps1 -databaseObjects $databaseObjects -patchDirectory $dir			
        }
	
    return $patchLocation
}

Write-Host "Binaries: $($expertBinariesDirectory)."
Write-Host "Output: $($outputDirectory)."

# Convert the magic pipeline variable $input from a enumerator to an array
$changeSet = @($input)

$zip = CreatePatch $changeSet 

Write-Host "Patch creation complete: $zip."