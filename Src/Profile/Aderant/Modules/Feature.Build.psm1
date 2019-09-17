# builds the current module using default parameters
function Start-BuildForCurrentModule([string]$clean, [bool]$debug, [bool]$release, [bool]$codeCoverage, [bool]$integration) {
    begin {
        Set-StrictMode -Version 'Latest'
        $ErrorActionPreference = 'Stop'
    }

    process {
        # Parameter must be a string as we are shelling out which we can't pass [switch] to
        [string]$commonArgs = "-moduleToBuildPath $ShellContext.CurrentModulePath -dropRoot $ShellContext.BranchServerDirectory -cleanBin $clean"

        if ($debug) {
            $commonArgs += " -debug"
        } elseif ($release) {
            $commonArgs += " -release"
        }

        if ($integration) {
            $commonArgs += " -integration"
        }

        if ($codeCoverage) {
            $commonArgs += " -codeCoverage"
        }

        Push-Location -Path $ShellContext.BuildScriptsDirectory
        Invoke-Expression -Command ".\BuildModule.ps1 $($commonArgs)"
        Pop-Location
    }
}

Export-ModuleMember Start-BuildForCurrentModule

<#
.Synopsis
    Jump up the file tree from the current location until the root of a git repository (contains a .git directory) is found.
.Outputs
    The path of the .git directory
#>
function Get-GitDirectory($searchDirectory) {
    # Canonicalize our starting location
    [string]$searchDirectory = [System.IO.Path]::GetFullPath($searchDirectory)

    do {
        # Construct the path that we will use to test against
        [string]$possibleFileDirectory = [System.IO.Path]::Combine($searchDirectory, '.git')

        # If we successfully locate the file in the directory that we're
        # looking in, simply return that location. Otherwise we'll
        # keep moving up the tree.
        if ([System.IO.Directory]::Exists($possibleFileDirectory)) {
            # We've found the file, return the directory we found it in
            return $searchDirectory
        } else {
            # GetDirectoryName will return null when we reach the root
            # terminating our search
            $searchDirectory = [System.IO.Path]::GetDirectoryName($searchDirectory)
        }
    } while ($null -ne $searchDirectory)

    # When we didn't find the location, then return an empty string
    return [string]::Empty
}

function Get-Dependencies {
    <#
    .Synopsis
        Retrieves the dependencies required to build the current module
        If run at the root will crawl all modules and get dependencies for everything
    #>
    [CmdletBinding()]
    param(
        [switch]$noUpdate,
        [switch]$showOutdated,
        [switch]$force
    )

    begin {
        Set-StrictMode -Version 'Latest'
        $ErrorActionPreference = 'Stop'
    }

    process {
        [string]$currentPath = (Get-Location).Path

        if ([System.IO.File]::Exists([System.IO.Path]::Combine($currentPath, 'Build\TFSBuild.proj'))) {
            & "$($global:ShellContext.BuildScriptsDirectory)\LoadDependencies.ps1" -modulesRootPath $currentPath -dropPath $global:ShellContext.BranchServerDirectory -update:$(-not $noUpdate.IsPresent) -showOutdated:$($showOutdated.IsPresent) -force:$($force.IsPresent)
        } else {
			[string]$root = Get-GitDirectory -searchDirectory $currentPath

			if ([string]::IsNullOrWhiteSpace($root)) {
			    Write-Error "Unable to locate .git directory based on path: '$($currentPath)'."
			    return
			}

            [string]$productManifest = Join-Path -Path $root -ChildPath 'Build\ExpertManifest.xml'

            if (-not (Test-Path $productManifest)) {
                Write-Error "Unable to locate ExpertManifest.xml at: '$productManifest'."
                return
            }

            [string]$branchConfigPath = Join-Path -Path $root -ChildPath 'Build\BranchConfig.xml'

            [string]$branchConfig = [string]::Empty
            if (Test-Path $branchConfigPath) {
                $branchConfig = Get-Content $branchConfigPath                
            } else {
                Write-Warning "Unable to locate BranchConfig.xml at: '$branchConfigPath'"
            }

            [string[]]$modulesInBuild = (Get-ChildItem -Path $root -Filter 'paket.dependencies' -File -Recurse -Depth 1).DirectoryName

            if ($null -eq $modulesInBuild -or $modulesInBuild.Count -eq 0) {
                Write-Error "Unable to locate any paket.dependencies files in any modules in root: '$root'."
                return
            }
			
            [Microsoft.Build.Framework.ITaskItem[]]$modulesInBuildTasks = $modulesInBuild | ForEach-Object { [Microsoft.Build.Utilities.TaskItem]::new($_) }

            [Aderant.Build.Tasks.GetDependencies]$getDependencies = [Aderant.Build.Tasks.GetDependencies]::new()

            [Aderant.Build.Logging.PowerShellLogger]$logger = [Aderant.Build.Logging.PowerShellLogger]::new((Get-Host))
            
            $getDependencies.ProductManifest = $productManifest
            $getDependencies.ConfigurationXml = $branchConfig
            $getDependencies.ModulesRootPath = $root
            $getDependencies.DropPath = $ShellContext.BranchServerDirectory
            $getDependencies.DependenciesDirectory = [System.IO.Path]::Combine($root, '_as\out')
            $getDependencies.ModulesInBuild = $modulesInBuildTasks
            $getDependencies.EnabledResolvers = @('NupkgResolver')

            $getDependencies.ExecuteInternal($logger)

            [Aderant.Build.Tasks.MakeSymlink]$makeSymlink = [Aderant.Build.Tasks.MakeSymlink]::new()
            $makeSymlink.Type = 'D'            

            $makeSymlink.Target = "$root\_as\out\packages"
            $makeSymlink.FailIfLinkIsDirectoryWithContent = $true
            $modulesInBuild | ForEach-Object {
                $makeSymlink.Link = "$_\packages"
                $makeSymlink.ExecuteInternal($logger)
            }

            $makeSymlink.Target = "$root\_as\out"
            $makeSymlink.FailIfLinkIsDirectoryWithContent = $false
            $modulesInBuild | ForEach-Object {
                $makeSymlink.Link = "$_\Dependencies"
                $makeSymlink.ExecuteInternal($logger)
            }
        }
    }
}

Export-ModuleMember Get-Dependencies

function Copy-BinariesFromCurrentModule() {
    if ([string]::IsNullOrEmpty($ShellContext.CurrentModulePath)) {
        Write-Warning "The current module is not set so the binaries will not be copied"
    } else {
        Push-Location -Path $ShellContext.BuildScriptsDirectory
        ResolveAndCopyUniqueBinModuleContent -modulePath $ShellContext.CurrentModulePath -copyToDirectory $ShellContext.BranchServerDirectory -suppressUniqueCheck $true
        Pop-Location
    }
}

Export-ModuleMember Copy-BinariesFromCurrentModule

<#
.Synopsis
    Builds a list of modules
.Description
    Automatically orders the modules according to their dependencies. Automatically handles
    copying dependencies between the list of modules. Use -getDependencies $true and -copyBinaries $true
    to get dependencies before local dependency management and starting each module build and/or to copy
    the output to the binaries location.
.PARAMETER workflowModuleNames
    An array of workflow module names
.PARAMETER changeset
    If specified will build all modules in the current changeset. This overrides workflowModuleNames
.PARAMETER getDependencies
    If specified, will call get-depedencies before copying the output from any other specified modules already built
    and running the build of this module.
.PARAMETER copyBinaries
    If specified, will copy the output of each module build to the binaries location.
.PARAMETER downstream
    If specified will build the sepcified modules and any modules which depend on them.
.PARAMETER getLatest
    If specified will get the latest source for the module from TFS before building.
.PARAMETER continue
    If specified will continue the last build starting at a build for the last module that failed
.PARAMETER getLocal
    If specified will get this comma delimited list of dependencies locally instead of from the drop folder
.PARAMETER exclude
    If specified will exclude this comma delimited list of modules from the build
.PARAMETER skipUntil
    A module name that if specified will build the list of modules as normal but skip the ones before the specified module
.EXAMPLE
        Build-ExpertModules
    Will build the current module. No dependencies will be prefetched and the output will not be copied to the binaries folder
.EXAMPLE
        Build-ExpertModules Libraries.Workflow
    Will build the "Libraries.Workflow" module. No dependencies will be prefetched and the output will not be copied to the binaries folder
.EXAMPLE
        Build-ExpertModules Libraries.Workflow, Libraries.Foundation, Libraries.Presentation
    Will build the specified modules in the correct order according to their dependencies (Libraries.Foundation, Libraries.Presentation, Libraries.Workflow).
    The output of each modules will be copied to the dependencies folder of the others before they are built, if are dependent.
    No dependencies will be prefetched and the output will not be copied to the binaries folder
.EXAMPLE
        Build-ExpertModules -changeset
    Will build the modules which have files currently checked out for edit. No dependencies will be prefetched and the output will not be copied to the binaries folder
.EXAMPLE
        Build-ExpertModules Libraries.Foundation -getLatest
    Will build the modules specified after get the latest source from TFS. No dependencies will be prefetched and the output will not be copied to the binaries folder
.EXAMPLE
        Build-ExpertModules Libraries.Foundation -getDependencies -copyBinaries -downstream
    Will build the specified module and any modules which directly or indirectly depend on it.
    The dependencies will be fetched before building and the output will be copied to the binaries folder.
.EXAMPLE
        Build-ExpertModules Libraries.Foundation -getDependencies -copyBinaries -downstream -skipUntil Libraries.Workflow
    will queue a build from the specified module and any modules which directly or indirectly depend on it but skip actually building any module
    until it reaches Libraries.Workflow. Useful for large builds that have failed somewhere in between and we want to pipck up from where we left off.
    The dependencies will be fetched before building and the output will be copied to the binaries folder.
#>
function Build-ExpertModules {
    param (
        [string[]]$workflowModuleNames, [switch] $changeset = $false, [switch] $clean = $false, [switch]$getDependencies = $false, [switch] $copyBinaries = $false, [switch] $downstream = $false, [switch] $getLatest = $false, [switch] $continue, [string[]] $getLocal, [string[]] $exclude, [string] $skipUntil, [switch]$debug, [switch]$release, [bool]$codeCoverage = $true, [switch]$integration, [switch]$codeCoverageReport
    )

    begin {
        Set-StrictMode -Version 'Latest'
        $InformationPreference = 'Continue'
    }

    process {
        if ($debug -and $release) {
            Write-Error "You can specify either -debug or -release but not both."
            return
        }

        if ($ShellContext.IsGitRepository) {
            Write-Error "You cannot run this command for a git repository. Use 'bm' or 'Invoke-Build' instead."
            return
        }

        $moduleBeforeBuild = $null

        try {
            $currentWorkingDirectory = Get-Location

            if (!$workflowModuleNames) {
                if (($ShellContext.CurrentModulePath) -and (Test-Path $ShellContext.CurrentModulePath)) {
                    $moduleBeforeBuild = (New-Object System.IO.DirectoryInfo $ShellContext.CurrentModulePath | ForEach-Object {$_.Name})
                    $workflowModuleNames = @($moduleBeforeBuild)
                }
            }

            $builtModules = @{}

            if (!$getLocal) {
                [string[]]$getLocal = @()
            }

            if (!$exclude) {
                [string[]]$exclude = @()
            }

            if ($continue) {
                if (!$global:LastBuildRemainingModules) {
                    Write-Information -MessageData "No previously failed build found"
                    return
                }

                $builtModules = $global:LastBuildBuiltModules
                $workflowModuleNames = $global:LastBuildRemainingModules
                $getDependencies = $global:LastBuildGetDependencies
                $copyBinaries = $global:LastBuildCopyBinaries
                $downstream = $global:LastBuildDownstream
                $getLatest = $global:LastBuildGetLatest
                $getLocal = $global:LastBuildGetLocal
                $exclude = $global:LastBuildExclude
            }

            if ($changeset) {
                Write-Information -MessageData [string]::Empty
                Write-Information -MessageData "Retrieving Expert modules for current changeset ..."
                [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$workflowModuleNames = $global:Workspace.GetModulesWithPendingChanges($ShellContext.BranchModulesDirectory)
                Write-Information -MessageData "Done."
            }

            # Set the new last build configuration
            $global:LastBuildGetDependencies = $getDependencies
            $global:LastBuildCopyBinaries = $copyBinaries
            $global:LastBuildDownstream = $downstream
            $global:LastBuildGetLatest = $getLatest
            $global:LastBuildRemainingModules = $workflowModuleNames
            $global:LastBuildGetLocal = $getLocal
            $global:LastBuildExclude = $exclude

            if (-not $workflowModuleNames) {
                Write-Information -MessageData "No modules specified."
                return
            }

            [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$workflowModuleNames = $global:Workspace.GetModules($workflowModuleNames)

            if ((Test-Path $ShellContext.BranchLocalDirectory) -ne $true) {
                Write-Information -MessageData "Branch Root path does not exist: '$ShellContext.BranchLocalDirectory'"
            }

            [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$modules = Sort-ExpertModulesByBuildOrder -BranchPath $ShellContext.BranchModulesDirectory -Modules $workflowModuleNames -ProductManifestPath $ShellContext.ProductManifestPath

            if (!$modules -or (($modules.Length -ne $workflowModuleNames.Length) -and $workflowModuleNames.Length -gt 0)) {
                Write-Warning "After sorting builds by order the following modules were excluded."
                Write-Warning "These modules probably have no dependency manifest or do not exist in the Expert Manifest"

                (Compare-Object -ReferenceObject $workflowModuleNames -DifferenceObject $modules -Property Name -PassThru) | Select-Object -Property Name

                $message = "Do you want to continue anyway?"
                $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes"
                $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No"

                $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)
                $result = $host.UI.PromptForChoice($null, $message, $options, 0)

                if ($result -ne 0) {
                    Write-Information -MessageData "Module(s) not found."
                    return
                }
            }

            if ($null -eq $exclude) {
                $exclude = @()
            }

            if ($downstream -eq $true) {
                Write-Information -MessageData ""
                Write-Information -MessageData "Retrieving downstream modules"

                [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$modules = $global:Workspace.DependencyAnalyzer.GetDownstreamModules($modules)

                $modules = Sort-ExpertModulesByBuildOrder -BranchPath $ShellContext.BranchModulesDirectory -Modules $modules -ProductManifestPath $ShellContext.ProductManifestPath
                $modules = $modules | Where-Object { $_.ModuleType -ne [Aderant.Build.DependencyAnalyzer.ModuleType]::Test }
                Write-Information -MessageData "Done."
            }

            $modules = $modules | Where-Object { $exclude -notcontains $_ }

            Write-Information -MessageData ""
            Write-Information -MessageData "********** Build Overview *************"
            $count = 0
            $weHaveSkipped = $false

            foreach ($module in $modules) {
                $count++
                $skipMarkup = ""

                if ($skipUntil -eq $module) {
                    $weHaveSkipped = $true
                }

                if ($skipUntil -and $weHaveSkipped -ne $true) {
                    $skipMarkup = " (skipped)"
                }

                Write-Information -MessageData "$count. $module $skipMarkup"
            }

            Write-Information -MessageData [string]::Empty
            Write-Information -MessageData [string]::Empty
            Write-Information -MessageData "Press Ctrl+C to abort"
            Start-Sleep -m 2000

            $weHaveSkipped = $false

            foreach ($module in $modules) {
                if ($skipUntil -eq $module) {
                    $weHaveSkipped = $true
                }

                # If the user specified skipUntil then we will skip over the modules in the list until we reach the specified one.
                if ($skipUntil -and $weHaveSkipped -eq $false) {
                    Write-Host "************* $module *************"
                    Write-Host "   Skipping  "
                    # Add the module to the list of built modules
                    if (!$builtModules.ContainsKey($module.Name)) {
                        $builtModules.Add($module.Name, $module)
                        $global:LastBuildBuiltModules = $builtModules
                    }
                } else {
                    # We either have not specified a skip or we have already skipped the modules we need to
                    Set-CurrentModule $module.Name

                    if ($getLatest) {
                        Get-LatestSourceForModule $module.Name -branchPath $ShellContext.BranchLocalDirectory
                    }

                    if ($getDependencies -eq $true) {
                        Get-Dependencies
                    }

                    if ($builtModules -and $builtModules.Count -gt 0) {
                        $dependencies = Get-ExpertModuleDependencies -BranchPath $ShellContext.BranchLocalDirectory -SourceModule $module -IncludeThirdParty $true
                        Write-Host "************* $module *************"

                        foreach ($dependencyModule in $dependencies) {
                            Write-Debug "Module dependency: $dependencyModule"

                            if (($dependencyModule -and $dependencyModule.Name -and $builtModules.ContainsKey($dependencyModule.Name)) -or ($getLocal | Where-Object { $_ -eq $dependencyModule })) {
                                $sourcePath = Join-Path $ShellContext.BranchLocalDirectory Modules\$dependencyModule\Bin\Module

                                if ($dependencyModule.ModuleType -eq [Aderant.Build.DependencyAnalyzer.ModuleType]::ThirdParty) {
                                    # Probe the new style ThirdParty path
                                    $root = [System.IO.Path]::Combine($ShellContext.BranchLocalDirectory, "Modules", "ThirdParty")

                                    if ([System.IO.Directory]::Exists($root)) {
                                        $sourcePath = [System.IO.Path]::Combine($root, $dependencyModule, "Bin")
                                    } else {
                                        # Fall back to the old style path
                                        $root = [System.IO.Path]::Combine($ShellContext.BranchLocalDirectory, "Modules")
                                        $sourcePath = [System.IO.Path]::Combine($root, $dependencyModule, "Bin")
                                    }
                                }

                                if (-not [System.IO.Directory]::Exists($sourcePath)) {
                                    throw "The path $sourcePath does not exist"
                                }

                                Write-Debug "Local dependency source path: $sourcePath"

                                $targetPath = Join-Path $ShellContext.BranchLocalDirectory Modules\$module
                                CopyContents $sourcePath "$targetPath\Dependencies"
                            }
                        }
                    }

                    # Do the Build
                    if ($module.ModuleType -ne [Aderant.Build.DependencyAnalyzer.ModuleType]::ThirdParty) {
                        Start-BuildForCurrentModule $clean $debug -codeCoverage $codeCoverage -integration $integration.IsPresent

                        Push-Location -Path $currentWorkingDirectory

                        # Check for errors
                        if ($LASTEXITCODE -eq 1) {
                            throw "Build of $module Failed"
                        } elseif ($LASTEXITCODE -eq 0 -and $codeCoverage -and $codeCoverageReport.IsPresent) {
                            [string]$codeCoverageReport = Join-Path -Path $ShellContext.CurrentModulePath -ChildPath "Bin\Test\CodeCoverage\dotCoverReport.html"

                            if (Test-Path ($codeCoverageReport)) {
                                Write-Host "Displaying dotCover code coverage report."
                                Start-Process $codeCoverageReport
                            } else {
                                Write-Warning "Unable to locate dotCover code coverage report."
                            }
                        }
                    }

                    # Add the module to the list of built modules
                    if (!$builtModules.ContainsKey($module.Name)) {
                        $builtModules.Add($module.Name, $module)
                        $global:LastBuildBuiltModules = $builtModules
                    }

                    # Copy binaries to drop folder
                    if ($copyBinaries -eq $true) {
                        Copy-BinariesFromCurrentModule
                    }
                }

                [string[]]$global:LastBuildRemainingModules = $global:LastBuildRemainingModules | Where-Object {$_ -ne $module}
            }

            $global:LastBuildRemainingModules = $null

            if ($moduleBeforeBuild) {
                cm $moduleBeforeBuild
            }
        } finally {
            Push-Location -Path $currentWorkingDirectory
            [Console]::TreatControlCAsInput = $false
        }
    }
}

Export-ModuleMember Build-ExpertModules