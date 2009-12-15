##
# Update the EnvironmentManifest with the correct source directory and then deploy
##
param ([string] $expertBinariesDirectory, [string] $environmentManifestPath)

begin{

    Function SetDeploymentSourceDirectoryInManifest($environmentManifestPath, $expertBinariesDirectory){    
        [xml]$environmentManifest =  Get-Content $environmentManifestPath      
        $environmentManifest.environment.SetAttribute("sourcePath", $expertBinariesDirectory)    
        $environmentManifest.Save($environmentManifestPath)    
    }

}

process{

    $currentDirectory = (Get-Location).Path

    write "$currentDirectory"

    SetDeploymentSourceDirectoryInManifest $environmentManifestPath $expertBinariesDirectory

    & "$expertBinariesDirectory\DeploymentEngine.exe" remove "$environmentManifestPath"

}






