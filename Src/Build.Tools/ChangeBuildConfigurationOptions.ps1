param([string]$buildType = "Release")

# This script represents a basic mechanism to update the build configuration section of all csproj files under a directory. 
# The script will look for various flags, such as "Optimize" and "DebugType". 
# If a flag is found which does not have an expected value it will be updated to ensure optimum runtime performance for Release, or for a rich debugging experience.

$projectSource = $BranchModulesDirectory

# The standard namespace for the csproj
$msbuild = "http://schemas.microsoft.com/developer/msbuild/2003"

# Known build configurations
$buildTypes = $null

# Flags that must be found in the Release build configuration 
$flags = New-Object 'System.Collections.Generic.Dictionary[String,String]'

if ($buildType.ToLower().Trim() -eq "release") {

    # setup Release build configuration
    $flags.Add('Optimize', 'true')           # Enable JIT optimizer
    $flags.Add('DebugType', 'pdbonly')       # Disable JIT tracing
    #$flags.Add('DebugSymbols', 'true')      # Deprecated?
    $buildTypes = @('Release|AnyCPU', 'Release|x64', 'Release|x86')
    
} else {

    # setup Debug build configuration
    $flags.Add('Optimize', 'false')          # Disable JIT optimizer    
    $flags.Add('DebugType', 'full')          # Enable JIT tracing (performance overhead)
    #$flags.Add('DebugSymbols', 'true')      # Deprecated?
    
    $buildTypes = @('Debug|AnyCPU', 'Debug|x64', 'Debug|x86')
    
}

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

function SanitizeBuildConfigurationElement($element, $project) {
    foreach ($key in $flags.Keys) {
        $e = $element.GetElementsByTagName($key)
        
        if ($e.Count -eq 0) {
            $warn = [System.String]::Format("'{0}' attribute found on project '{1}' [Build Configuration: {2}]", $key, $project.Project.PropertyGroup[0].AssemblyName, $buildType)
            Write-Warning $warn
            
            # Create a new blank element in the MSBuild namespace. The value will be set later
            $childElement = $project.CreateElement($key, $msbuild)
            $childElement.InnerText = "" 
            $element.AppendChild($childElement)            
            continue            
        }
        
        # Sometimes there are duplicates, remove them all except one which we will then update
        RemoveDuplicateNodes $element $e
    }
}

function ChangeBuildType($element, $file, $project) {
    foreach ($key in $flags.Keys) {
        $e = $element.GetElementsByTagName($key)
        
        if ($e.Item(0).InnerText -ne $flags[$key]) {
            $message = [System.String]::Format("An expected value was not found... updating '{0}' to '{1}'", $e.Item(0).InnerText, $flags[$key])
            
            #if ($e.Item(0) -ne $null) {
                $e.Item(0).InnerXml = $flags[$key]
            
                # Check out the file and save it... could add a check-in hook here
                Invoke-Expression "tf checkout $file"
                $project.Save($file)
           # }
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
        foreach ($type in $buildTypes) {
        
            $query = "//dns:PropertyGroup[contains(@Condition, '$type')]"

            $nsmgr = new-object Xml.XmlNamespaceManager($proj.PSBase.NameTable)
            $nsmgr.AddNamespace("dns", $msbuild)
            $nodes = $proj.SelectNodes($query, $nsmgr)

            if ($nodes.count -gt 0) {
                $file = [System.IO.Path]::GetFileName($item)
                Write-Host "Processing $file"
                
                $element = $nodes.Item(0)                
                
                SanitizeBuildConfigurationElement $element $proj
                ChangeBuildType $element $item $proj
            }
        }
    }
}

# Get all csprog files from the modules directory
$items = (ls $projectSource -Recurse *.csproj) | % { $_.FullName } 

# Run it!
SwitchBuildType