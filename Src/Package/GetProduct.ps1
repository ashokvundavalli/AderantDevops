##
# For each module in the product manifest we gets all built <ModuleName>\Bin\Module from the $dropRoot and puts
# it into the $binariesDirectory. The factory .bin will be created from what exists in the $binariesDirectory
#
# Flag $getDebugFiles will get all pdb's so that you can debug
##
<# 
.Synopsis 
    Pull from the drop location all source and assocated tests of those modules that are defined in the given product manifest  
.Description    
    For each module in the product manifest we get the built ouput from <ModuleName>\Bin\Test and <ModuleName>\Bin\Module 
    and puts it into the $binariesDirectory. 
    The factory .bin will be created from what exists in the $binariesDirectory
.Example     
    GetProduct -$productManifestPath C:\Source\Dev\<branch name>\ExpertManifest.xml -$dropRoot \\na.aderant.com\expertsuite\dev\<branch name> -$binariesDirectory C:\Source\Dev\<branch name>\Binaries
    GetProduct -$productManifestPath C:\Source\Dev\<branch name>\ExpertManifest.xml -$dropRoot \\na.aderant.com\expertsuite\dev\<branch name> -$binariesDirectory C:\Source\Dev\<branch name>\Binaries -$getDebugFiles $true
.Parameter $productManifestPath is the path to the product manifest that defines the modules that makeup the product
.Parameter $dropRoot is the path drop location that the binaries will be fetched from
.Parameter $binariesDirectory the directory you want the binaries to be copied too
.Parameter $systemMapConnectionString for creation of customization in the format of /pdbs:<server> /pdbd:<expertdatabase> /pdbid:<userid> /pdbpw:<userpassword>
.Parameter $onlyUpdated only gets the modules that have changed since the last get-product
#>

param([string]$productManifestPath,
      [string]$dropRoot,
      [string]$binariesDirectory, 
      [bool]  $getDebugFiles=$false,      
      [string]$systemMapConnectionString,
      [switch]$onlyUpdated) 

begin {  
    $ErrorActionPreference = "Stop"

    write $MyInvocation.MyCommand.Name 

    Write-Debug "buildLibrariesPath: $buildLibrariesPath"

    $buildScriptsDirectory = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
    $buildLibraries = "$buildScriptsDirectory\..\Build\Build-Libraries.ps1"
    & $buildLibraries

    LoadLibraryAssembly "$buildScriptsDirectory\..\Build"   
    
    Function global:GenerateFactory([string]$inDirectory, [string]$searchPath){
        write "Generating factory in [$inDirectory]"        
        &$inDirectory\FactoryResourceGenerator.exe /v /f:$inDirectory /of:$inDirectory/Factory.bin $searchPath                        
    }
    
    ###
    # Optionally run function that will generate the customization sitemap
    ###
    Function global:GenerateSystemMap([string]$inDirectory, [string]$systemMapArgs) {
        Write-Debug "About to generate the sitemap"                        
        
        if ($systemMapArgs -like "*/pdbs*" -and $systemMapArgs -like "*/pdbd*" -and $systemMapArgs -like "*/pdbid*" -and $systemMapArgs -like "*/pdbpw*") {           
            $connectionParts = $systemMapArgs.Split(" ")                                            
            Write-Host "SystemMap connection is [$connectionParts]"
           
            &$inDirectory\Systemmapbuilder.exe /f:$inDirectory /o:$inDirectory\systemmap.xml /ef:Customization $connectionParts[0] $connectionParts[1] $connectionParts[2] $connectionParts[3]        
        } else {
            throw "Connection string is invalid for use with SystemMapBuilder.exe [$systemMapArgs]"
        }                                                              
    }
    
    ###
    # Move files required for the prerequisite installer into the drop location
    ###
    Function MoveApplicationServerPrerequisitesToFolder([string] $rootPath) {
        $prerequisitesDropPath = Join-Path $rootPath 'ApplicationServerPrerequisites'
        write "Moving app server prerequisite files to $prerequisitesDropPath"
        CreateDirectory $prerequisitesDropPath
        CreateDirectory $(Join-Path $prerequisitesDropPath 'Tools')
        CreateDirectory $(Join-Path $prerequisitesDropPath 'Installers')

        MoveItem $(Join-Path $rootPath 'PrerequisitesPowerShell\ExpertApplicationServer.ps1') $(Join-Path $prerequisitesDropPath 'ExpertApplicationServer.ps1')
        MoveItem $(Join-Path $rootPath 'NTRights.exe') $(Join-Path $prerequisitesDropPath 'Tools\NTRights.exe')
        MoveItem $(Join-Path $rootPath 'NDP461-KB3102436-x86-x64-AllOS-ENU.exe') $(Join-Path $prerequisitesDropPath 'Installers\NDP461-KB3102436-x86-x64-AllOS-ENU.exe')
        MoveItem $(Join-Path $rootPath 'WindowsServerAppFabricSetup_x64.exe') $(Join-Path $prerequisitesDropPath 'Installers\WindowsServerAppFabricSetup_x64.exe')
        MoveItem $(Join-Path $rootPath 'AppFabric-KB3092423-x64-ENU.exe') $(Join-Path $prerequisitesDropPath 'Installers\AppFabric-KB3092423-x64-ENU.exe')
		MoveItem $(Join-Path $rootPath 'rewrite_amd64.msi') $(Join-Path $prerequisitesDropPath 'Installers\rewrite_amd64.msi')
    }

    Function GetPackagingExcludeList([xml]$productManifest, [string]$productManifestPath) {
        $nodes = $productManifest.SelectNodes("//ProductManifest/Modules/Module")
        return (Get-ExpertModulesToExcludeFromPackaging -Manifest $nodes -ExpertManifestPath $productManifestPath)
    }
}

process {
    $newdropFolderBuildNumbers = @()    
    
    if (Test-Path "$binariesDirectory\DropFolderBuildNumbers.txt") {
        $dropFolderBuildNumbers = Get-Content "$binariesDirectory\DropFolderBuildNumbers.txt"
    }

    if ((-not $onlyUpdated) -or ($dropFolderBuildNumbers -eq $null)) {
        if (Test-Path $binariesDirectory) {                
            Remove-Item $binariesDirectory\* -Recurse -Force -Exclude "environment.xml"
        }
    } else {
        if (Test-Path $binariesDirectory) {                
            Remove-Item $binariesDirectory\* -Force -Exclude "environment.xml","DropFolderBuildNumbers.txt", ExpertSource
        }
    }

    if (-not $onlyUpdated) {
        # Was having an issue with the ExpertSource folder not being removed correctly and causing issue when getting the dlls.
        # Sleep stops this from happening
        Start-Sleep -m 1500
    }

    #Create ExpertSource and Deployment Folders
    $expertSourceDirectory = Join-Path $binariesDirectory 'ExpertSource'
    if (-not $onlyUpdated) {
        CreateDirectory $expertSourceDirectory
    }

    [xml]$productManifest = Get-Content $productManifestPath    

    # These modules should be excluded from packaging as they are included and packaged by the build process of the module that consumes them.
    $excludeList = GetPackagingExcludeList $productManifest $productManifestPath

	# create dependencies file for Paket operation
	$dependenciesContentBuilder = New-Object -TypeName System.Text.StringBuilder -ArgumentList "source http://packages.ap.aderant.com/packages/nuget"
    $dependenciesContentBuilder.AppendLine() | Out-Null

    foreach ($module in $productManifest.SelectNodes("//ProductManifest/Modules/Module")) {
        if ($module.ExcludeFromPackaging) {
            Write-Warning "Excluding $($module.Name) from product"
            continue
        }

        [string]$moduleBinariesDirectory = GetPathToBinaries $module $dropRoot
        if ([string]::IsNullOrWhiteSpace($moduleBinariesDirectory)) { #If we could not find a last successful drop path
            if ($module.Name -eq "Tests.UIAutomation") {
                Write-Warning "No drop path for $($module.Name) you need to build this module."
                continue
            }
            throw "Unable to retrieve the binaries for $($module.Name)"
        } else {
            $moduleBinariesDirectory = $moduleBinariesDirectory.Trim()
        }

        $skipModule = $false
        foreach ($excludedModule in $excludeList) {
            if ($excludedModule.Name -ieq $module.Name) {

                $items = Get-ChildItem $moduleBinariesDirectory -Filter *.dll

                if ($items.Count -eq 0) {                    
                    $skipModule = $true;
                }
            }
        }

        if ($skipModule -eq $true) {
            Write-Warning "Excluding $($module.Name) from product"
            continue
        }

		# add Paket dependency for third party module
		if (IsThirdParty $module) {
			# TODO: grab paket.lock files and calculate versions to get from the nuget server here
			# TODO: set the $newdropFolderBuildNumbers for third party modules to the nuget version rather that the last write time
			# TODO:	where is the DropFolderBuildNumbers.txt file used?
			# TODO: do we need to change the ExpertManifest.xml file for third party module entries?

			$dependenciesContentBuilder.AppendLine([string]::Concat("nuget ", $module.Name.Replace("Thirdparty", "ThirdParty"))) | Out-Null

			continue
		}

		write "Getting bin for $($module.Name)"
		# If onlyUpdated switch is entered we only copy modules that have changed since last time we copied them
		if ($onlyUpdated) {
			$areBinariesUpToDate = $false
			for ($i = 0; $i -lt $dropFolderBuildNumbers.Length; $i++) {
				if ($dropFolderBuildNumbers[$i].StartsWith($module.Name)) {
					if ( ($module.Name.Contains("Help")) -and -not ($module.Name.Contains("Admin")) ) {
                    
						# Check the date for Help/Admin modules
						$modifiedDate = ((Get-Item $moduleBinariesDirectory).LastWriteTimeUtc).ToString("o")
						if ($modifiedDate -like $dropFolderBuildNumbers[$i].Split("=")[1]){
							Write-Host -ForegroundColor Gray "Skipping " $module.Name " as it is up to date."
							$newdropFolderBuildNumbers += $dropFolderBuildNumbers[$i]
							$areBinariesUpToDate = $true
							break
						}
					} else {
						try {
							# Check the drop build number for Aderant modules
							$dropBuildNumber = LatestSuccesfulBuildNumber $module $dropRoot
						} catch {
							$dropBuildNumber = ((Get-Item $moduleBinariesDirectory).LastWriteTimeUtc).ToString("o")
						}
						if ($dropBuildNumber -like $dropFolderBuildNumbers[$i].Split("=")[1]) {
							Write-Host -ForegroundColor Gray "Skipping " $module.Name " as it is up to date."
							$newdropFolderBuildNumbers += $dropFolderBuildNumbers[$i]
							$areBinariesUpToDate = $true
							break
						}
					}
				}
			}

			if ($areBinariesUpToDate -and -not $module -eq "Libraries.Roles") {
				continue
			}
		}
        
        if (Test-Path $moduleBinariesDirectory) {
            CopyModuleBinariesDirectory $moduleBinariesDirectory $expertSourceDirectory $getDebugFiles          
        } else {
            Throw "Failed trying to copy output for $moduleBinariesDirectory" 
        }	

        if ( ($module.Name.Contains("Help")) -and -not ($module.Name.Contains("Admin")) )  {
            $newdropFolderBuildNumbers += $module.Name + "=" + ((Get-Item $moduleBinariesDirectory).LastWriteTimeUtc).ToString("o")
        } else {
            try {
                $newdropFolderBuildNumbers += $module.Name + "=" + (LatestSuccesfulBuildNumber $module $dropRoot)
            } catch {
                $newdropFolderBuildNumbers += $module.Name + "=" + ((Get-Item $moduleBinariesDirectory).LastWriteTimeUtc).ToString("o")
            }
        }
    }

	# handle third party modules by getting them via Paket
	$paketDependenciesFile = [System.IO.Path]::Combine($expertSourceDirectory, "paket.dependencies");
		[System.IO.File]::WriteAllText($paketDependenciesFile, $dependenciesContentBuilder.ToString());
    
    $paket = [System.IO.Path]::Combine($buildScriptsDirectory, "..\Build\paket.exe")
    if (-not (Test-Path $paket)) {
        throw "Cannot find paket.exe"
    }

	$paketProcess = Start-Process -FilePath $paket -ArgumentList "install" -WorkingDirectory $expertSourceDirectory -Wait
	
	# don't need the paket.dependencies and .lock files any more
	$paketLockFile = [System.IO.Path]::Combine($expertSourceDirectory, "paket.lock")
	Remove-Item $paketDependenciesFile
	Remove-Item $paketLockFile #-Force -ErrorAction SilentlyContinue

	Generate-ThirdPartyAttributionFile $expertSourceDirectory

	# copy third party files to ExpertSource
	foreach ($thirdPartyModuleFolder in [System.IO.Directory]::EnumerateDirectories([System.IO.Path]::Combine($expertSourceDirectory, "packages"))) {
        $source = [System.IO.Path]::Combine($thirdPartyModuleFolder, "lib")
		CopyModuleBinariesDirectory $source $expertSourceDirectory $getDebugFiles
		Remove-Item -Recurse -Force $thirdPartyModuleFolder
	}    

    RemoveReadOnlyAttribute $binariesDirectory
    
    MoveApplicationServerPrerequisitesToFolder $expertSourceDirectory
    
    GenerateFactory $expertSourceDirectory "/sp:Aderant*.dll`,*.exe"    
    if ([string]::IsNullOrEmpty($systemMapConnectionString) -ne $true) {
        GenerateSystemMap $expertSourceDirectory $systemMapConnectionString
    }
        
    MoveDeploymentFiles $productManifest.ProductManifest.ExpertVersion $binariesDirectory $expertSourceDirectory
    MoveInternalFiles $productManifest.ProductManifest.ExpertVersion $expertSourceDirectory
    
    $newdropFolderBuildNumbers | Out-File $binariesDirectory\DropFolderBuildNumbers.txt
}

end {
    $doneMessage = "Product "+ $productManifest.ProductManifest.Name +" retrieved"
    write ""
    write $doneMessage
}
