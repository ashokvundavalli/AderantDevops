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
    function global:GenerateFactory([string]$inDirectory, [string]$searchPath){
        Write-Host "Generating factory in [$inDirectory]"

        return Start-Job -Name "Generate Factory" -ScriptBlock { 
            param($directory, $searchPath, $logDirectory) 
            & $directory\FactoryResourceGenerator.exe /v /f:$directory /of:$directory/Factory.bin $searchPath > "$logDirectory\FactoryResourceGenerator.log"
        } -Argument $inDirectory,$systemMapArgs,$script:LogDirectory
    }
    
    ###
    # Optionally run function that will generate the customization sitemap
    ###
    function global:GenerateSystemMap([string]$inDirectory, [string]$systemMapArgs) {
        Write-Host "About to generate the system map"                        
        
        if ($systemMapArgs -like "*/pdbs*" -and $systemMapArgs -like "*/pdbd*" -and $systemMapArgs -like "*/pdbid*" -and $systemMapArgs -like "*/pdbpw*") {                         
            return Start-Job -Name "Generate System Map" -ScriptBlock { 
                param($directory, $systemMapArgs, $logDirectory) 

                $connectionParts = $systemMapArgs.Split(" ")                                            
                Write-Host "System Map builder arguments are [$connectionParts]"       

                & $directory\SystemMapBuilder.exe /f:$directory /o:$directory\systemmap.xml /ef:Customization $connectionParts[0] $connectionParts[1] $connectionParts[2] $connectionParts[3] > "$logDirectory\SystemMap.log"
            } -Argument $inDirectory,$systemMapArgs,$script:LogDirectory
            
        } else {
            throw "Connection string is invalid for use with SystemMapBuilder.exe [$systemMapArgs]"
        }                                                              
    }
    
    ###
    # Move files required for the prerequisite installer into the drop location
    ###
    function MoveApplicationServerPrerequisitesToFolder([string] $rootPath) {
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

    function GetPackagingExcludeList([xml]$productManifest, [string]$productManifestPath) {
        $nodes = $productManifest.SelectNodes("//ProductManifest/Modules/Module")
        return (Get-ExpertModulesToExcludeFromPackaging -Manifest $nodes -ExpertManifestPath $productManifestPath)
    }
    
    function SkipModule() {
        [CmdletBinding()]
        param([string]$moduleBinariesDirectory, $module, $excludelist)
        
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
        if ($excludeList) {
            foreach ($excludedModule in $excludeList) {
                if ($excludedModule.Name -ieq $module.Name) {

                    $items = Get-ChildItem $moduleBinariesDirectory -Filter *.dll -ErrorAction SilentlyContinue
                
                    if ($items -eq $null -or $items.Count -eq 0) {                    
                        $skipModule = $true
                    }
                }
            }
        }

        if ($skipModule -eq $true) {        
            return $skipModule
        }

        return $skipModule
    }

    <#
        Write license attribution content (license.txt) to the product directory         
    #>
    Function global:Generate-ThirdPartyAttributionFile([string[]]$licenseText, [string]$expertSourceDirectory) {
        Write-Output "Generating ThirdParty license file."

        $attributionFile = Join-Path $expertSourceDirectory 'ThirdPartyLicenses.txt'        
        
        foreach ($license in $licenseText) {
            Add-Content -Path $attributionFile -Value $license -Encoding UTF8
        }

        if (-not (Test-Path $attributionFile)) {
            throw "Third Party license file not generated"
        }

        Write-Output "Attribution file: $attributionFile"
    }

    function RetreiveModules() {
        [CmdletBinding()]
        param([string]$productManifestPath, $modules, [string[]]$folders, [string]$expertSourceDirectory) 

        [string[]]$modules = $modules | Select-Object -ExpandProperty Name

        $result = Package-ExpertRelease -ProductManifestPath $productManifestPath -Modules $modules -Folders $folders -ProductDirectory $expertSourceDirectory
        if ($result) {
            Generate-ThirdPartyAttributionFile $result.ThirdPartyLicenses $expertSourceDirectory
        }
    }  
    
    function WaitForJobs() {
        while ((Get-Job).State -match 'Running') {
        $jobs = Get-Job | Where-Object {$_.HasMoreData}

        if ($jobs) {
            $message = [string]::Join(",", ($jobs | Select-Object -ExpandProperty "Name"))
            Write-Host "Waiting for job[s]: $message..."
        }

        foreach ($job in $jobs) {
            Receive-Job $Job
        }
                
        Start-Sleep -Seconds 5
        }        
    }

    $ErrorActionPreference = "Stop"

    Write-Host $MyInvocation.MyCommand.Name 

    Write-Debug "buildLibrariesPath: $buildLibrariesPath"

    $buildScriptsDirectory = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
    $buildLibraries = "$buildScriptsDirectory\..\Build\Build-Libraries.ps1"
    & $buildLibraries

    LoadLibraryAssembly "$buildScriptsDirectory\..\Build"
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

    $script:LogDirectory = [System.IO.Path]::Combine($binariesDirectory, "Logs")
    (New-Item -Path ($script:LogDirectory) -ItemType Directory -ErrorAction SilentlyContinue) | Out-Null    

    if (-not $onlyUpdated) {
        # Was having an issue with the ExpertSource folder not being removed correctly and causing issue when getting the dlls.
        # Sleep stops this from happening
        Start-Sleep -m 1500
    }

    # Create ExpertSource
    $expertSourceDirectory = Join-Path $binariesDirectory 'ExpertSource'
    if (-not $onlyUpdated) {
        CreateDirectory $expertSourceDirectory | Out-Null
    }

    [xml]$productManifest = Get-Content $productManifestPath    
        
    $excludeList = $null
    $modules = $productManifest.SelectNodes("//ProductManifest/Modules/Module")
       
    $folders = @()  
    foreach ($module in $modules | Where-Object -Property GetAction -ne "NuGet") { 
        Write-Host "Processing $($module.Name)"       
        if (IsThirdParty $module) {
            continue;
        }

        if ($module.ExcludeFromPackaging) {
            Write-Warning "Excluding $($module.Name) from product"
            continue
        }

        [string]$moduleBinariesDirectory = GetPathToBinaries $module $dropRoot
        
        if (Test-Path $moduleBinariesDirectory) {            
            $folders += $moduleBinariesDirectory
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

    $packagedModules = $modules | Where-Object { $_.GetAction -eq "NuGet" -or (IsThirdParty $_) }    
    RetreiveModules $productManifestPath $packagedModules $folders $expertSourceDirectory    

    RemoveReadOnlyAttribute $binariesDirectory
    
    GenerateFactory $expertSourceDirectory "/sp:Aderant*.dll`,*.exe" | Out-Null

    if ([string]::IsNullOrEmpty($systemMapConnectionString) -ne $true) {
        GenerateSystemMap $expertSourceDirectory $systemMapConnectionString | Out-Null
    }   

    WaitForJobs

    MoveApplicationServerPrerequisitesToFolder $expertSourceDirectory
    MoveDeploymentFiles $productManifest.ProductManifest.ExpertVersion $binariesDirectory $expertSourceDirectory
    #MoveInternalFiles $productManifest.ProductManifest.ExpertVersion $expertSourceDirectory
    
    $newdropFolderBuildNumbers | Out-File $binariesDirectory\DropFolderBuildNumbers.txt
}

end {
    $doneMessage = "Product "+ $productManifest.ProductManifest.Name + " retrieved"
    write ""
    write $doneMessage
}
