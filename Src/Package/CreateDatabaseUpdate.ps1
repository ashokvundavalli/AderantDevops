<#  
.Synopsis
    Creates an Expert database update (UPD).
.Parameter databaseObjects
    An array of database objects to include from the database project. The full Team Foundation URI is expected.
.Parameter patchDirectory
    The root directory of the patch. This is the output location of the UPD and database objects.       
#>
param(
    [string[]]$databaseObjects,
    [string]$patchDirectory    
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
function CreateUpdateScript($objects, $patchLocation) {
    $versionControl = [Aderant.BuildTools.PatchBuilder.ProjectFactory]::VersionControl
	
	[xml]$manifest = Get-Content .\PatchingManifest.xml
	$name = $manifest.Settings.PatchName
	$name = $name.Replace(" ", "").Trim()
    
    $updateRoot = [System.IO.Path]::Combine($patchLocation, "Database", "Expert")
    $updateScripts = [System.IO.Path]::Combine($updateRoot, $name)
    
    Remove-Item $updateScripts -Recurse -Force -ErrorAction SilentlyContinue
         
    $scriptsDirectory = New-Item -Path $updateScripts -ItemType Directory -Force    
    Write-Host "Created directory: $scriptsDirectory."
    
    CreateRunFile $updateRoot $name
    
    $updateItems = @()    
    
    # Filter by name as it's unique       
	foreach ($dbObject in $objects | Sort-Object -Unique -Property @{Expression={ [System.IO.Path]::GetFileName($_) }}) {
        $fileName = [System.IO.Path]::GetFileName($dbObject)
        
        # Remove dots as DBGEN doesn't like them
        $localFile = [System.IO.Path]::Combine($scriptsDirectory, $fileName.Replace(".sql", "").Replace(".", "_"))
        $localFile = $localFile + ".sql"
		
        $versionControl.DownloadFile($dbObject, $localFile)
        
        Write-Host "Downloaded $dbObject to $localFile from version control."
        
        AddDropStatement $localFile
                       
        $updateItems += $localFile
    }
    
    $script = CreateUpdateScriptText $updateItems $name   
       
    [System.IO.File]::WriteAllText([System.IO.Path]::Combine($scriptsDirectory, "$name.upd"), $script)   
    
    Write-Host "Database update components created."
}

function CreateRunFile([string]$path, [string]$runFileFor) {
$script = [System.String]::Format('Code {0}
Description "Golden Gate {0} Release"
TruncateLog


Version 75
  Description ""
End

Update {0}.UPD
  Path "{0}"
End


', $runFileFor)    

    $runFile = [System.IO.Path]::Combine($path, "EXPERT_1.upd")
    [System.IO.File]::WriteAllText($runFile, $script)
    Write-Host "Run file created: $runFile"
}

function CreateUpdateScriptText($updateItems, $updateName) {
    Write-Host "Creating database update with name: $name."
    
    $sb = New-Object System.Text.StringBuilder
    
    [void]$sb.AppendFormat('Code {0}
Description ""

Version 75
  Description ""
End', $updateName)

    [void]$sb.Append([System.Environment]::NewLine)
    [void]$sb.Append([System.Environment]::NewLine)
    [void]$sb.Append([System.Environment]::NewLine)
    
    # Now add the body of the UPD
    foreach ($item in $updateItems) {
        [void]$sb.AppendFormat('Script "{0}"
  Description
  Process "UPDATE"
  Repeatable
End', [System.IO.Path]::GetFileName($item))

        [void]$sb.Append([System.Environment]::NewLine)
        [void]$sb.Append([System.Environment]::NewLine)
    }
    
    return $sb.ToString()
}


function AddDropStatement($file) {
    # Items in source control do not have ALTER or DROP statements. We must add them for each script.
    $contents = [System.IO.File]::ReadAllText($file)
    
    # ... I don't know what I'm doing with RegEx :)
    $pattern = "\[(.*?)\]";
    $matches = [System.Text.RegularExpressions.Regex]::Matches($contents, $pattern)
    
    $schema = $matches[0].Value
    $object = $matches[1].Value
    
    if ($schema -eq $null) {
        throw (New-Object System.ArgumentNullException("schema", "A schema name could not be extracted from the script $file."))
    }
    
    if ($object -eq $null) {
        throw (New-Object System.ArgumentNullException("$object", "A object name could not be extracted from the script $file."))
    }
   
    $output = $null
    $drop = $null
    
    if ($contents.ToLowerInvariant().Contains("create view") -or $file.EndsWith("_view.sql")) {   
        $drop = "if exists (select * from sys.views where object_id = object_id(N'$schema.$object'))
drop view $schema.$object
go"
    }
    
    if ($contents.ToLowerInvariant().Contains("create proc") -or $file.EndsWith("_proc.sql")) {
        $drop = "if exists (select * from sys.objects where object_id = object_id(N'$schema.$object') and type in (N'P', N'PC'))
drop procedure $schema.$object
go"   
    }   

    if ($drop -ne $null) {
        $sb = New-Object System.Text.StringBuilder   
        [void]$sb.AppendLine($drop)
        [void]$sb.AppendLine()
        [void]$sb.AppendLine()
        [void]$sb.AppendLine($contents)
        [void]$sb.AppendLine()
        [void]$sb.AppendLine()
        [void]$sb.AppendLine("GO") # extra magic, DBGEN needs a carefully placed GO directive or it will complain
                
        Write-Host "Updated script $([System.IO.Path]::GetFileName($file)) with drop statement." -ForegroundColor Magenta        
        [System.IO.File]::WriteAllText($file, $sb.ToString())
    } else {
        Write-Warning "Script $([System.IO.Path]::GetFileName($file)) was not updated with drop statement." 
    }
}

CreateUpdateScript $databaseObjects $patchDirectory