<# 
.Synopsis 
    Imports the packages specified for an automation test run.
.Example     
    RemoteImportPackage "vmaklexpdevb03.ap.aderant.com" ".\SkadLove.CustomizationTest.environment.xml" "\\VMAKLEXPDEVB04\ExpertPackages\FileOpening.zip"
.Parameter $remoteMachineName is the fully qualified domain name of the remote machine
.Parameter $environmentManifestpath is the path to the environment manifest file
#>

param ([string]$environmentManifestPath, 
       [string]$packageDirectory,
       [switch]$licenses,
       [switch]$workflows)

process{
    if (-not $licenses -and -not $workflows) {
        Write-Warning "No packages will be imported. You need to specify whether you want to import licenses or workflows or both."
    }
    [xml]$environment = Get-Content $environmentManifestPath;
    $appServer = $environment.environment.servers.server.name
    $items = @();
    if ($licenses) {
        $licenseFiles = Get-Item $packageDirectory\* -Filter license*
        foreach ($each in $licenseFiles) {
            $items += $each;
        }
    }
    if ($workflows) {
        $workflowFiles = Get-Item $packageDirectory\* -Filter Workflow*
        foreach ($each in $workflowFiles) {
            $items += $each
        }
    }
    foreach($each in $items) {
        .\RemoteImportPackage.ps1 -remoteMachineName $appServer -environmentManifestPath $environmentManifestPath -packagePath $each.FullName
    }
        
	#Write-Host "Last Exit Code : " + $LASTEXITCODE

}