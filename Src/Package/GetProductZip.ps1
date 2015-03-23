##
# Copies the latest successful build all zip file and extracts it to $binariesDirectory
##
<# 
.Synopsis 
    Pull from the drop location the latest build all output. 
.Description    
    Copies the latest successful build all zip file and extracts it to $binariesDirectory
.Example     
    GetProductZip -$productManifestPath C:\Source\Dev\<branch name>\ExpertManifest.xml -$dropRoot \\na.aderant.com\expertsuite\dev\<branch name> -$binariesDirectory C:\Source\Dev\<branch name>\Binaries
.Parameter $remoteMachineName is then ame of the remote machine to copy and unzip binaries to.
.Parameter $dropRoot is the path drop location that the binaries will be fetched from
.Parameter $binariesDirectory the directory you want the binaries to be copied to on the remote machine.
.Parameter $buildLibrariesPath
#>
param( [string] $remoteMachineName,
       [string] $dropRoot,
       [string] $binariesDirectory,
       [string] $buildLibrariesPath)

begin{  

    
    ###
    # Get the common Build-Libraries
    ###
    Function LoadLibraries([string]$buildInfrastructurePath){
        $buildLibrariesPath = Join-Path -Path $buildInfrastructurePath.Trim() -ChildPath \Build\Build-Libraries.ps1
        Write-Host "Loading Build Libraries from $buildLibrariesPath"
        &($buildLibrariesPath)
    }
    
    Function ResolveBuildInfrastructurePath($buildLibrariesPath){
        [string]$buildInfrastructureSrcPath
        if([String]::IsNullOrEmpty($buildLibrariesPath) -eq $true){
            $buildInfrastructureSrcPath = (Join-Path $dropRoot "Build.Infrastructure\Src\")
        }else{
            return $buildLibrariesPath
        }                                 
        return $buildInfrastructureSrcPath
    }
        
    # Load common library functions.
    [string]$buildInfrastructurePath = ResolveBuildInfrastructurePath($buildLibrariesPath)
    LoadLibraries -buildInfrastructurePath $buildInfrastructurePath        
    
    # Import remote helper module.
	$modulePath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Definition) RemoteDeploymentHelper.psm1
    Import-Module $modulePath
        
}

process{
    write "GetProductZip.ps1"        
	$ErrorActionPreference = "Stop"
	$session = Get-RemoteSession $remoteMachineName
    
    Write-Host "Finding latest successful product zip in [$dropRoot]"
    $zipName = "ExpertBinaries.zip"
	[string]$pathToZip = (PathToLatestSuccessfulPackage -pathToPackages $dropRoot -packageZipName $zipName)
    $pathToZip = $pathToZip.Trim()
    Write-Host "Found latest successful product zip at [$pathToZip]"
    
    #On the remote machine, copy the zip and extract it.
    Write-Host "Invoking remote script to copy and unzip on remote machine [$remoteMachineName]"
 	Invoke-Command $session `
        -ScriptBlock {
            param($binariesDirectory, $pathToZip, $zipName)
            if(Test-Path $binariesDirectory){                
                Remove-Item $binariesDirectory\* -Recurse -Force -Exclude "environment.xml"
            }
            Write-Host "...Copying [$pathToZip] to [$binariesDirectory]"
            Copy-Item -Path $pathToZip -Destination $binariesDirectory
            $localZip =  (Join-Path $binariesDirectory $zipName)
            Write-Host "...Extracting zip to [$binariesDirectory]"
        	$shellApplication = new-object -com shell.application
        	$zipPackage = $shellApplication.NameSpace($localZip)
        	$destinationFolder = $shellApplication.NameSpace($binariesDirectory)
        	$destinationFolder.CopyHere($zipPackage.Items())
        	Write-Host "...Finished extracting zip"
        } `
        -ArgumentList $binariesDirectory, $pathToZip, $zipName
    
}

end{
    $doneMessage = "Product "+ $product.ProductManifest.Name +" retrieved"
    write $doneMessage
}

