##
# Update the EnvironmentManifest with the correct source directory and then deploy
##
param ([string] $expertBinariesDirectory, [string] $environmentManifestPath, [string] $remoteMachinename)

begin{
    
    Function SetDeploymentSourceDirectoryInManifest($environmentManifestPath, $expertBinariesDirectory){    
        [xml]$environmentManifest =  Get-Content $environmentManifestPath      
        $environmentManifest.environment.SetAttribute("sourcePath", $expertBinariesDirectory)    
        $environmentManifest.Save($environmentManifestPath)    
    }

    Function TryKillProcess([string] $processName, [string] $computerName){
        Invoke-Command -ComputerName $computerName -ArgumentList $processName -ScriptBlock {
            param([string] $processName)
            begin{
                $process =  Get-Process -ErrorAction "SilentlyContinue" $processName 
                if ($process) {
                    write "Stopping $processName"
                    $process | Stop-Process -force
                }
            }
        }
    }

}

process{
    $ErrorActionPreference = "Stop"
    $currentDirectory = (Get-Location).Path
    write "$currentDirectory"
    SetDeploymentSourceDirectoryInManifest $environmentManifestPath $expertBinariesDirectory
    & "$expertBinariesDirectory\DeploymentEngine.exe" stop "$environmentManifestPath"
    Start-Sleep -s 60
    TryKillProcess "Expert.Workflow.Service" $remoteMachinename
    TryKillProcess "ExpertMatterPlanning" $remoteMachinename
    TryKillProcess "ConfigurationManager" $remoteMachinename
    Start-Sleep -s 30
    & "$expertBinariesDirectory\DeploymentEngine.exe" remove "$environmentManifestPath"

}






