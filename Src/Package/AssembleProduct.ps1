<#
.Synopsis
    Pull from the drop location all source and associated tests of those modules that are defined in the given product manifest.
.Description
    For each module in the product manifest we get the built ouput from <ModuleName>\Bin\Test and <ModuleName>\Bin\Module
    and puts it into the $binariesDirectory.
    The factory .bin will be created from what exists in the $binariesDirectory
.Example
    & '.\AssembleProduct.ps1' -productManifestPath 'C:\Source\ExpertSuite\Build\ExpertManifest.xml' -binariesDirectory 'C:\AderantExpert\Binaries'
.Parameter productManifestPath
    The path to the product manifest that defines the modules that makeup the product
.Parameter binariesDirectory
    The directory you want the binaries to be copied to.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$productManifestPath,
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$binariesDirectory
)

begin {
    Set-StrictMode -Version 'Latest'
    $ErrorActionPreference = 'Stop'
    $InformationPreference = 'Continue'

    Write-Information -MessageData "Running '$($MyInvocation.MyCommand.Name.Replace(`".ps1`", `"`"))' with the following parameters:"
    Write-Information ($PSBoundParameters | Format-Table | Out-String)

    [bool]$global:skipEndpointCheck = $true
    . "$PSScriptRoot\..\Build\Functions\Initialize-BuildEnvironment.ps1"

    function IsThirdparty {
        <#
        .SYNOPSIS
            Is this a ThirdParty module?
        #>
        [CmdletBinding()]
        [OutputType([bool])]
        param (
            [Parameter(Mandatory=$true)][ValidateNotNull()][System.Xml.XmlNode]$module
        )

        [string]$name = $null

        if ($module.GetType().FullName -like "System.Xml*") {
            $name = $module.Name
        } else {
            $name = $module
        }

        return $name -like 'thirdparty.*'
    }

    function PathToLatestSuccessfulBuild {
        <#
        .SYNOPSIS
            Find the last successfully build in the drop location.
        #>
        [CmdletBinding()]
        param (
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$pathToModuleAssemblyVersion,
            [switch]$suppressThrow
        )

        begin {
            function CheckBuild {
                <#
                .SYNOPSIS
                    Checks tail of a build log.  If build successful returns True.
                #>
                [CmdletBinding()]
                param (
                    [Parameter(Mandatory=$true)][string]$buildLog
                )

                if (Test-Path $buildLog) {
                    $noErrors = Get-Content $buildLog | Select-Object -last 10 | Where-Object {$_.Contains("0 Error(s)")}

                    if ($noErrors) {
                       return $true
                    } else {
                       return $false
                    }
                } else {
                    Write-Warning "No build log to check at path: '$buildLog'."
                }
            }

            function SortedFolders {
                <#
                .SYNOPSIS
                    Pads each section of the folder name (which is in the format 1.8.3594.41082) with zeroes, so that an alpha sort
                    can be used because each section will now be of the same length.
                #>
                [CmdletBinding()]
                param (
                    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$parentFolder
                )

                begin {
                    function IsAllNumbers {
                        [CmdletBinding()]
                        param (
                            [Parameter(Mandatory=$true)][ValidateNotNull()]$container
                        )

                        $numbers = $container.Name.Replace(".", "")

                        $rtn = $null
                        if ([int64]::TryParse($numbers, [ref]$rtn)) {
                            return $true
                        }

                        Write-Debug "Folder $($container.FullName) is not a valid build drop folder"
                        return $false
                    }
                }

                process {
                    if (Test-Path $parentFolder) {
                        $sortedFolders =  (Get-ChildItem -Path $parentFolder |
                                    Where-Object {$_.PsIsContainer} |
                                    Where-Object { IsAllNumbers $_ } |
                                    Sort-Object {$_.name.Split(".")[0].PadLeft(4,"0")+"."+ $_.name.Split(".")[1].PadLeft(4,"0")+"."+$_.name.Split(".")[2].PadLeft(8,"0")+"."+$_.name.Split(".")[3].PadLeft(8,"0")+"." } -Descending  |
                                    Select-Object name)

                        return $sortedFolders
                    } else {
                        Write-Debug "$parentFolder could not be found because it does not exist"
                    }
                }
            }
        }

        process {
            $sortedFolders = SortedFolders $pathToModuleAssemblyVersion

            [bool]$noBuildFound = $true
            [string]$pathToLatestSuccessfulBuild = $null

            foreach ($folderName in $sortedFolders) {
                [string]$pathToLatestSuccessfulBuild = Join-Path -Path $pathToModuleAssemblyVersion -ChildPath $folderName.Name
                [string]$successfulBuildBinModule = Join-Path -Path $pathToLatestSuccessfulBuild -ChildPath "\Bin\Module"

                Write-Debug "Considering: $pathToLatestSuccessfulBuild"

                $isSuccessfulBuild = $false
                if (Test-Path (Join-Path -Path $pathToLatestSuccessfulBuild -ChildPath "build.succeeded")) {
                     $isSuccessfulBuild = $true
                }

                if (Test-Path $successfulBuildBinModule) {
                    $pathToLatestSuccessfulBuild = $successfulBuildBinModule
                }

                if ($isSuccessfulBuild) {
                    Write-Debug "Returning: $pathToLatestSuccessfulBuild"
                    return $pathToLatestSuccessfulBuild
                }

                [string]$buildLog = Join-Path -Path (Join-Path -Path $pathToModuleAssemblyVersion -ChildPath $folderName.Name) -ChildPath "\BuildLog.txt"
                if (Test-Path $buildLog) {
                    if (CheckBuild $buildLog) {
                        return $pathToLatestSuccessfulBuild
                    }
                }
            }

            if ($noBuildFound -and -not $suppressThrow.IsPresent) {
                throw "No latest build found for $pathToModuleAssemblyVersion"
            }

            return $null
        }
    }

    function AcquireExpertClassicBinaries {
        [CmdletBinding()]
        param (
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$moduleName,
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$binariesDirectory,
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$classicPath,
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$target
        )

        $classicPath = PathToLatestSuccessfulBuild $classicPath -suppressThrow

        if ([string]::IsNullOrWhiteSpace($classicPath)) {
            Write-Warning "Unable to acquire binaries for module: $moduleName"
            return
        }

        $build = Get-ChildItem -LiteralPath $classicPath -File -Filter "*.zip"

        $destinationFolder = $null

        if ($null -ne $build) {
            [string]$zipExe = [System.IO.Path]::GetFullPath((Join-Path -Path $PSScriptRoot -ChildPath '\..\Build.Tools\7za.exe'))

            if (Test-Path $zipExe) {
                [string]$filter = [string]::Empty

                if ([string]::Equals('Expert.Classic.CS', $moduleName, [System.StringComparison]::OrdinalIgnoreCase)) {
                    $filter = 'ApplicationServer\*'
                }

                $destinationFolder = Join-Path -Path $binariesDirectory -ChildPath $target

                Start-Process -FilePath $zipExe -ArgumentList "x `"$($build.FullName)`" -o`"$destinationFolder`" $filter -x!*.pdb -r -y" -NoNewWindow -Wait

                [string]$classicBuildNumbersFile = "$($binariesDirectory)\ClassicBuildNumbers.txt"
                Add-Content -LiteralPath $classicBuildNumbersFile -Value "$($moduleName) $($build.BaseName.split('_')[1])" -Force
                Write-Information -MessageData "Successfully acquired Expert Classic binaries $($build.Directory.Name)"
            } else {
                Write-Error "Unable to locate tool: '$zipExe'."
            }
        } else {
            Write-Error "Unable to acquire Expert Classic binaries from: $($classicPath)"
        }
    }

    function AcquireExpertClassicDocumentation {
        [CmdletBinding()]
        param(
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$moduleName,
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$moduleBinariesDirectory,
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$binariesDirectory
        )

        try {
            [System.IO.FileSystemInfo]$pdfBuild = Get-ChildItem -Path $moduleBinariesDirectory -Directory | Sort-Object -Property Name | Select-Object -First 1

            if ($null -ne $pdfBuild -and (Test-Path -Path (Join-Path -Path $pdfBuild.FullName -ChildPath "Pdf"))) {
                [System.IO.FileSystemInfo[]]$pdfBuildDir = Get-ChildItem -Path (Join-Path -Path $pdfBuild.FullName -ChildPath "Pdf") -Filter "*.pdf"

                if ($null -ne $pdfBuildDir -and (Measure-Object -InputObject $pdfBuildDir) -ne 0) {
                    try {
                        Copy-Item -Path "$($pdfBuild.FullName)\Pdf" -Recurse -Filter "*.pdf" -Destination (Join-Path -Path $binariesDirectory -ChildPath $module.Target.Split('/')[1]) -Force
                        [string]$classicBuildNumbersFile = "$($binariesDirectory)\ClassicBuildNumbers.txt"
                        Add-Content -LiteralPath $classicBuildNumbersFile -Value "$($moduleName) $($pdfBuild.Name)" -Force

                        return $pdfBuild.Name
                    } catch {
                        Write-Warning "Unable to acquire content from: '$($pdfBuild.FullName)\Pdf' for module: $($module.Name)"
                        return $null
                    }
                }
            }
        } catch {
        }

        Write-Warning "Unable to acquire content for module: $($module.Name)"
        return $null
    }

    function GetPathToBinaries {
        <#
        .SYNOPSIS
            Resolves the path to the binaries for the given module.
        #>
        [CmdletBinding()]
        param (
            [Parameter(Mandatory=$true)][ValidateNotNull()][System.Xml.XmlElement]$module
        )

        if ($module.HasAttribute("GetAction")) {
            if ($module.GetAction.Equals("specificdroplocation", [System.StringComparison]::OrdinalIgnoreCase)) {
                if ($module.Name.EndsWith(".pdf", [System.StringComparison]::OrdinalIgnoreCase)) {
                    return ([System.IO.Path]::Combine($module.Path, $module.Name))
                }

                return $module.Path
            } else {
                throw "Invalid GetAction specified for module: $($module.GetAction)"
            }
        } else {
            Write-Warning "Module: $($module.Name) has no GetAction attribute specified."
        }
    }

    function GenerateThirdPartyAttributionFile {
        <#
        .Synopsis
            Write license attribution content (license.txt) to the product directory.
        #>
        param (
            [string[]]$licenseText,
            [string]$expertSourceDirectory
        )

        Write-Information -MessageData 'Generating ThirdParty license file.'
        [string]$attributionFile = [System.IO.Path]::Combine($expertSourceDirectory, 'ThirdPartyLicenses.txt')

        foreach ($license in $licenseText) {
            Add-Content -Path $attributionFile -Value $license -Encoding 'UTF8'
        }

        if (-not (Test-Path $attributionFile)) {
            throw 'Third Party license file not generated.'
        }
        Write-Information -MessageData "Attribution file: $attributionFile"
    }

    function RetreiveModules {
        [CmdletBinding()]
        param (
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$productManifestPath,
            [Parameter(Mandatory=$true)][ValidateNotNull()][Xml]$productManifestXml,
            [Parameter(Mandatory=$true)][ValidateNotNull()]$modules,
            [Parameter(Mandatory=$false)][string[]]$folders,
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$expertSourceDirectory
        )

        try {
            $result = Package-ExpertRelease -ProductManifestPath $productManifestPath -ProductManifestXml $productManifestXml.InnerXml -Modules $modules -Folders $folders -ProductDirectory $expertSourceDirectory
        } catch [Exception] {
            Write-Error "PackageExpertReleaseCommand failed. Exception: '$($_.Exception.Message)' Stack trace: '$($_.Exception.StackTrace)'."
            throw
        }

        if ($result) {
            GenerateThirdPartyAttributionFile $result.ThirdPartyLicenses $expertSourceDirectory
        }
    }


}

process {
    if (Test-Path $binariesDirectory) {
        Remove-Item "$binariesDirectory\*" -Recurse -Force -Exclude 'environment.xml', 'cms.ini'
    }

    $script:LogDirectory = [System.IO.Path]::Combine($binariesDirectory, 'Logs')
    [void](New-Item -Path ($script:LogDirectory) -ItemType 'Directory' -ErrorAction 'SilentlyContinue')

    [string]$expertSourceDirectory = [System.IO.Path]::Combine($binariesDirectory, 'ExpertSource')
    [void](New-Item -ItemType 'Directory' -Path $expertSourceDirectory -Force)

    [xml]$productManifest = Get-Content -LiteralPath $productManifestPath

    [System.Xml.XmlNodeList]$modules = $productManifest.SelectNodes("//ProductManifest/Modules/Module")

    $folders = @()
    foreach ($module in $modules) {
        if ($module.PSObject.Properties.Name -match 'GetAction') {
            if ($module.GetAction -eq 'NuGet') {
                continue
            }
        }
        if (IsThirdParty -module $module) {
            Write-Information -MessageData "Ignored module: $($module.Name)"
            continue
        }

        if ($module.PSObject.Properties.Name -eq 'ExcludeFromPackaging') {
            if ($module.ExcludeFromPackaging -eq $true) {
                Write-Information "Excluding module: '$($module.Name)' from product."
                continue
            }
        }

        Write-Information -MessageData "Resolving latest version for module: '$($module.Name)'."

        [string]$moduleBinariesDirectory = GetPathToBinaries -module $module

        if ([string]::IsNullOrWhiteSpace($moduleBinariesDirectory)) {
            continue
        }

        if ($module.Name -match "Expert.Classic") {
            if ($module.Name.EndsWith('pdf', [System.StringComparison]::OrdinalIgnoreCase)) {
                AcquireExpertClassicDocumentation -moduleName $module.Name -moduleBinariesDirectory $moduleBinariesDirectory -binariesDirectory $binariesDirectory

                continue
            } else {
                AcquireExpertClassicBinaries -moduleName $module.Name -binariesDirectory $binariesDirectory -classicPath $moduleBinariesDirectory -target $module.Target.Split('/')[1]
            }

            continue
        }

        $folders += $moduleBinariesDirectory
    }

    $packagedModules = $modules | Where-Object { $_.PSObject.Properties.Name -match 'GetAction' -and $_.GetAction -eq "NuGet" -or (IsThirdParty $_) }

    RetreiveModules -ProductManifestPath $productManifestPath -productManifestXml $productManifest -modules $packagedModules -folders $folders -expertSourceDirectory $expertSourceDirectory

    attrib.exe -r $binariesDirectory /d

    #region Post-process
    [string]$manifestDirectory = [System.IO.Path]::GetDirectoryName($productManifestPath)
    [string]$postProcessScript = Join-Path -Path $manifestDirectory -ChildPath 'AssembleProduct.ps1'

    if (Test-Path -Path $postProcessScript) {
        Write-Information -MessageData "Executing script: '$postProcessScript'."
        & $postProcessScript -BinariesDirectory $binariesDirectory
    }
    #endregion

    Get-ChildItem -Path $binariesDirectory -Depth 0 -File | Where-Object { -not ($_.Name.EndsWith(".msi") -or ($_.BaseName.Contains("ClassicBuildNumbers"))) } | Remove-Item -Force -Exclude "environment.xml","DropFolderBuildNumbers.txt"

    Write-Information -MessageData "Product $($productManifest.ProductManifest.Name) retrieved."
}