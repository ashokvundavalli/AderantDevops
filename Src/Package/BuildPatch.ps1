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

<# 
.Synopsis 
    Creates a DBGEN UPD (Update) package from a collection of source control links.
.Description
.Parameter objects
    The collection of links to build the update package for.
.Parameter patchLocation
	The drop location of the patch.
#>
function CreateDatabaseUpdate($objects, $patchLocation) {
    $sb = New-Object System.Text.StringBuilder
    
    $dir = [System.IO.Path]::Combine($patchLocation, "Database", "Expert")
    Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
         
    $scriptsDirectory = New-Item -Path $dir -ItemType Directory -Force    
    Write-Host "Created directory: $scriptsDirectory."
    
    $versionControl = [Aderant.BuildTools.PatchBuilder.ProjectFactory]::VersionControl
	
	[xml]$manifest = Get-Content .\PatchingManifest.xml
	$name = $manifest.Settings.PatchName
	$name = $name.Replace(" ", "").Trim()
	
	Write-Host "Creating UPD header with name: $name."
 
 $header = 'Code ' + $name +'
Description ""

Version 75
  Description "' + $name + ' Release"
End

'
	[void]$sb.Append($header)
    
    # Defend against cross branch objects (filter by name as it's unique)
    $objects = $objects | Sort-Object -Unique -Property @{Expression={ [System.IO.Path]::GetFileName($_) }}
    
	foreach ($dbObject in $objects) {
        $fileName = [System.IO.Path]::GetFileName($dbObject)
        
        # Remove dots as DBGEN doesn't like them
        $localFile = [System.IO.Path]::Combine($scriptsDirectory, $fileName.Replace(".sql", "").Replace(".", "_"))
        $localFile = $localFile + ".sql"
		
        $versionControl.DownloadFile($dbObject, $localFile)
	
		Write-Host "Downloaded $dbObject to $localFile from version control."
        
        PrefixDatabaseObject $localFile        
        
		# Now add the body of the UPD
        $updItem = 'Script "' + [System.IO.Path]::GetFileName($localFile) + '"
  Description ""
  Process "UPDATE"
  Repeatable
End


'
        [void]$sb.Append($updItem)
	}
    
    $upd = [System.IO.Path]::Combine($scriptsDirectory, "EXPERT_1.UPD")
    
    [System.IO.File]::WriteAllText($upd, $sb.ToString())
    Write-Host "Database update created: $upd"
}

# Helper to workout if this file is a database object
function IsDatabaseObject($file) {
    return ($file.StartsWith("$") -and $file.ToLower().EndsWith(".sql"))
}

# Items in source control do not have ALTER or DROP statements. We must add them for each script.
function PrefixDatabaseObject($file) {
	#stub
}

function AddPackageFile($package, $file) { 
    $files = New-Object System.Collections.ObjectModel.Collection[Aderant.Framework.Packaging.Process.IncludeFile]

    if ([System.IO.File]::Exists($file)) {
       $includeFile = New-Object Aderant.Framework.Packaging.Process.IncludeFile($file, [System.String]::Empty, $false)
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


function ExportPackage($packager, $package) {
    Write-Host "Exporting package: $($package.Definition.Name)."
    
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
        Write-Host "Error while attempting to create patch: $($_.Exception.Message)."
    } finally {
        Write-Host "Cleaning up package directory."
        $packager.RemovePackagingDirectory()
        
        Write-Host "Cleaning up temporary directory."
        if ([System.IO.Directory]::Exists($packager.OutputDirectory)) {
            [System.IO.Directory]::Delete($packager.OutputDirectory, $true)
        }
    }
	return $outzip
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
    
    # Load the packaging assemblies to build the zip for us
    try {
        Write-Host "Loading packaging assemblies from $expertBinariesDirectory."
        [void][System.Reflection.Assembly]::LoadFrom([System.IO.Path]::Combine($expertBinariesDirectory, "Aderant.Framework.dll"))
        [void][System.Reflection.Assembly]::LoadFrom([System.IO.Path]::Combine($expertBinariesDirectory, "Aderant.Framework.Packaging.Process.dll"))
        [void][System.Reflection.Assembly]::LoadFrom([System.IO.Path]::Combine($expertBinariesDirectory, "Aderant.Framework.Packaging.Client.dll"))
    } catch [System.Exception] {
        Write-Error "Error while loading packaging assemblies in build output. Error: $($_.Exception.Message)."
        throw
    }
    
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
        Write-Host "Change $($change.Id) contains $($change.Projects.Count) affected projects."        
        $change.Projects | ForEach-Object { $projects += $_ }
    }
    
    # Projects might be referenced by many change sets, build a unique list of projects for the current changes
    $projects = $projects | Sort-Object -Property ProjectFile -Unique
    
    $files = @()
    $databaseObjects = @()
    
    foreach ($project in $projects) {    
        if (![String]::IsNullOrEmpty($project.AssemblyFileName)) {            
            $files += [System.IO.Path]::Combine($expertBinariesDirectory, $project.AssemblyFileName)
        }
        
        foreach ($artifact in $project.Artifacts) {
            if (IsDatabaseObject($artifact)) {
                $databaseObjects += $artifact
            } else {
                $files += [System.IO.Path]::Combine($expertBinariesDirectory, $artifact)
            }
        }
    }
    
    $files | Sort-Object -Unique | ForEach-Object { AddPackageFile $package $_ }
	    
    $patchLocation = ExportPackage $packager $package
	
        if ($databaseObjects.Count -gt 0) {
        $dir = [System.IO.Path]::GetDirectoryName($patchLocation)
                CreateDatabaseUpdate $databaseObjects $dir
        }
	
        return $patchLocation
}

Write-Host "Binaries: $($expertBinariesDirectory)."
Write-Host "Output: $($outputDirectory)."

# Convert the magic pipeline variable $input from a enumerator to an array
$changeSet = @($input)

$zip = CreatePatch $changeSet 

Write-Host "Patch creation complete: $zip."