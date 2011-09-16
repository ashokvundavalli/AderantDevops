# This script represents a basic mechanism to update the release build configuration section of all csproj files under a directory. 
# The script will look for various flags. If a flag is found which does not have an expected value it will be updated to ensure 
# optimum runtime performance.

$projectSource = $BranchModulesDirectory

# The standard namespace for the csproj
$msbuild = "http://schemas.microsoft.com/developer/msbuild/2003"

# Known build configurations
$releaseBuildTypes = @('Release|AnyCPU', 'Release|x64', 'Release|x86')

# Flags that must be found in the Release build configuration 
$flags = New-Object 'System.Collections.Generic.Dictionary[String,String]'
$flags.Add('Optimize', 'true')
$flags.Add('DebugType', 'pdbonly')

# Items that should not have the csproj modified
$skipPattern = @(
'UnitTest', 
'ThirdParty', 
'Modules\Workflow.', 
'IntegrationTest', 
'UITest', 
'UXCatalog',
'Test')

function RemoveDuplicateNodes($parent, $nodes) {
    for ($i = $nodes.Count-1; $i -ge 1; $i--) {
        $parent.RemoveChild($nodes.Item($i))
    }
}

function SanitizeReleaseConfigurationElement($element, $project) {
    foreach ($key in $flags.Keys) {
        $e = $element.GetElementsByTagName($key)
        
        if ($e.Count -eq 0) {
            Write-Warning "No $key flag found"
            #$project.CreateElement($key)
            continue            
        }
        
        RemoveDuplicateNodes $element $e
    }
}

function ChangeBuildType($element, $file, $project) {
    foreach ($key in $flags.Keys) {
        $e = $element.GetElementsByTagName($key)
        
        if ($e.Item(0).InnerText -ne $flags[$key]) {
            $message = [System.String]::Format("An expected value was not found... updating '{0}' to '{1}'", $e.Item(0).InnerText, $flags[$key])
            
            $e.Item(0).InnerXml = $flags[$key]     
            
            Invoke-Expression "tf checkout $file"
            $project.Save($file)
        }
    }
}


function SwitchBuildType() {

    foreach ($item in $items) {
        $isCheckedOut = $false
        $skip = $false
        
        foreach ($pattern in $skipPattern) {
            if ($item.Contains($pattern)) { 
                $skip = $true
            }
        }
        
        if ($skip -eq $true) {
            continue
        }
        
        # Load the csporj
        $proj = [xml](Get-Content $item)
        
        # Check the csproj for all known build types as it may define more than one
        foreach ($type in $releaseBuildTypes) {
        
            $query = "//dns:PropertyGroup[contains(@Condition, '$type')]"

            $nsmgr = new-object Xml.XmlNamespaceManager($proj.PSBase.NameTable)
            $nsmgr.AddNamespace("dns", $msbuild)
            $nodes = $proj.SelectNodes($query, $nsmgr)

            if ($nodes.count -gt 0) {
                $file = [System.IO.Path]::GetFileName($item)
                Write-Host "$file defines a build type of $type"
                
                $element = $nodes.Item(0)
                
                # Sometimes there are duplicates, remove them all except one which
                # we will then update
                SanitizeReleaseConfigurationElement $element $proj
                ChangeBuildType $element $item $proj
            }
        }
    }
}

# Get all csprog files from the modules directory
$items = (ls $projectSource -Recurse *.csproj) | % { $_.FullName } 

# Run it!
SwitchBuildType