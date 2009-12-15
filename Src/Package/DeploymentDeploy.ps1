param ([string] $expertBinariesDirectory, [string] $environmentManifestPath)

begin{

    Function SetDeploymentSourceDirectory($environmentManifestPath){    
        [xml]$environmentManifest =  Get-Content $environmentManifestPath      
        $environmentManifest.environment.SetAttribute("sourcePath", $expertBinariesDirectory)    
        $environmentManifest.Save($environmentManifestPath)    
    }
}

process{

    $currentDirectory = (Get-Location).Path

    write "$currentDirectory"

    if ([System.IO.Directory]::Exists("$currentDirectory\Expert")) {
        remove-item -path "$currentDirectory\Expert" -recurse -force
    }
    
    SetDeploymentSourceDirectory $environmentManifestPath

    new-item -path "$currentDirectory" -name "Expert" -type directory -force | out-null

    copy-item -path "$expertBinariesDirectory\*" -destination "$currentDirectory\Expert" -recurse -force

    set-location -path "$currentDirectory\Expert"

    & "$currentDirectory\Expert\DeploymentEngine.exe" deploy "$environmentManifestPath"

    set-location -path "$currentDirectory"

    if ([System.IO.Directory]::Exists("$currentDirectory\Expert")) {
        remove-item -path "$currentDirectory\Expert" -recurse -force
    }
}