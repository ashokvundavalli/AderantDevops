function Get-GitDirectory($searchDirectory) {
    <#
    .Synopsis
        Jump up the file tree from the current location until the root of a git repository (contains a .git directory) is found.
    .Outputs
        The path of the .git directory
    #>

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
    .SYNOPSIS
        Retrieves NuGet packages.
    .DESCRIPTION
        If run at the root, the command will retrieve NuGet packages for subdirectories if applicable to the repository.
    .PARAMATER NoSymlinks
        This switch will avoid using a shared dependency directory.
    .PARAMETER Force
        This switch will invoke Paket in update mode.
    #>
    [CmdletBinding()]
    param(
        [switch]$NoSymlinks,
        [switch]$Force
    )

    begin {
        Set-StrictMode -Version 'Latest'
        $ErrorActionPreference = 'Stop'

        function CreateSymlinks {
            <#
            .SYNOPSIS
                Creates symlinks between directories and a shared dependency directory.
            .DESCRIPTION
                Creates symlinks for Dependencies and Packages to a shared dependency directory.
            .PARAMETER dependenciesDirectory
                The directory to symlink to.
            .PARAMETER directories
                The directories to create symlinks from.
            #>
            [CmdletBinding()]
            param (
                [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$dependenciesDirectory,
                [Parameter(Mandatory=$true)][ValidateNotNull()][string[]]$directories
            )

            [Aderant.Build.Tasks.MakeSymlink]$makeSymlink = [Aderant.Build.Tasks.MakeSymlink]::new()
            $makeSymlink.Type = 'D'

            $makeSymlink.Target = [System.IO.Path]::Combine($root, $dependenciesDirectory, 'packages')
            $makeSymlink.FailIfLinkIsDirectoryWithContent = $true
            $directories | ForEach-Object {
                [string]$path = "$_\packages"
                    
                if ([System.IO.Directory]::Exists($path)) {
                    [System.IO.DirectoryInfo]$directory = Get-Item -Path $path -Force
                    if ([bool]($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                        Write-Debug "Symbolic link already present at path: '$path'."
                        continue
                    }
                }

                $makeSymlink.Link = $path
                $makeSymlink.ExecuteInternal($logger)
            }

            $makeSymlink.Target = [System.IO.Path]::Combine($root, $dependenciesDirectory)
            $makeSymlink.FailIfLinkIsDirectoryWithContent = $false
            $directories | ForEach-Object {
                [string]$path = "$_\Dependencies"

                if ([System.IO.Directory]::Exists($path)) {
                    [System.IO.DirectoryInfo]$directory = Get-Item -Path $path -Force
                    if ([bool]($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                        Write-Debug "Symbolic link already present at path: '$path'."
                        continue
                    }
                }

                $makeSymlink.Link = $path
                $makeSymlink.ExecuteInternal($logger)
            }
        }
    }

    process {
        [string]$currentPath = (Get-Location).Path
        [string]$root = Get-GitDirectory -searchDirectory $currentPath
        [bool]$origin = [string]::Equals($root, $currentPath, [System.StringComparison]::OrdinalIgnoreCase)

		if ([string]::IsNullOrWhiteSpace($root)) {
			Write-Error "Unable to locate .git directory based on path: '$($currentPath)'."
			return
		}

        [string]$productManifest = [System.IO.Path]::Combine($root, 'Build\ExpertManifest.xml')
        if (-not [System.IO.File]::Exists($productManifest)) {
            Write-Debug "ExpertManifest.xml does not exist at path: '$productManifest'."
            $productManifest = $null
        }

        [string]$branchConfigPath = Join-Path -Path $root -ChildPath 'Build\BranchConfig.xml'
        [System.Xml.XmlDocument]$branchConfig = $null
        if ([System.IO.File]::Exists($branchConfigPath)) {
            $branchConfig = Get-Content -Path $branchConfigPath
        } else {
            Write-Debug "BranchConfig.xml does not exist at path: '$branchConfigPath'."
            $branchConfigPath = $null
        }

        [string[]]$modulesInBuild = $null
        if ($origin) {
            if ([System.IO.File]::Exists([System.IO.Path]::Combine($root, 'paket.dependencies'))) {
                $modulesInBuild = @($root)
            } else {
                $modulesInBuild = (Get-ChildItem -Path $root -Filter 'paket.dependencies' -File -Recurse -Depth 1).DirectoryName
            }
        } else {
            if ([System.IO.File]::Exists([System.IO.Path]::Combine($currentPath, 'paket.dependencies'))) {
                $modulesInBuild = @($currentPath)
            } else {
                $modulesInBuild = @($root)
            }
        }

        if ($null -eq $modulesInBuild -or $modulesInBuild.Count -eq 0) {
            Write-Error "Unable to locate any paket.dependencies files in any modules in root: '$root'."
            return
        }

        [string]$dependenciesDirectory = $null
        [bool]$update = $false

        if (-not $NoSymlinks.IsPresent) {
            if ($null -ne $branchConfig -and $branchConfig.BranchConfig.GetElementsByTagName('DependenciesDirectory') -and -not [string]::IsNullOrWhiteSpace($branchConfig.BranchConfig.DependenciesDirectory)) {
                $dependenciesDirectory = $branchConfig.BranchConfig.DependenciesDirectory
            }
        }

        if (-not $origin -or -not [string]::IsNullOrWhiteSpace($dependenciesDirectory)) {
            $update = $true
        }

        if ($Force.IsPresent) {
            if (-not [string]::IsNullOrWhiteSpace($dependenciesDirectory)) {
                # Remove the generated paket.dependencies file if it exists.
                [string]$paketDependencies = [System.IO.Path]::Combine($root, $dependenciesDirectory, 'paket.dependencies')

                if ([System.IO.File]::Exists($paketDependencies)) {
                    Write-Debug "Removing paket.dependencies file: '$paketDependencies'."
                    [System.IO.File]::Delete($paketDependencies)
                }
            }

            $update = $true
        }

        [Microsoft.Build.Framework.ITaskItem[]]$modulesInBuildTasks = $modulesInBuild | ForEach-Object { [Microsoft.Build.Utilities.TaskItem]::new($_) }
        [Aderant.Build.Tasks.GetDependencies]$getDependencies = [Aderant.Build.Tasks.GetDependencies]::new()
        [Aderant.Build.Logging.PowerShellLogger]$logger = [Aderant.Build.Logging.PowerShellLogger]::new((Get-Host))

        $getDependencies.ProductManifest = $productManifest
        $getDependencies.BranchConfigFile = $branchConfigPath
        $getDependencies.ModulesRootPath = $root
        $getDependencies.ModulesInBuild = $modulesInBuildTasks
        $getDependencies.EnabledResolvers = @('NupkgResolver')
        $getDependencies.Update = $update

        if ($NoSymlinks.IsPresent) {
            $getDependencies.DisableSharedDependencyDirectory = $true
        }

        $getDependencies.ExecuteInternal($logger)

        if (-not $NoSymlinks.IsPresent -and -not [string]::IsNullOrWhiteSpace($dependenciesDirectory)) {
            CreateSymlinks -dependenciesDirectory $dependenciesDirectory -directories $modulesInBuild
        }

        [string]$editorConfig = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\..\.editorconfig")

        foreach ($module in $modulesInBuild) {
            Write-Debug "Updating .editorconfig file in module: '$module'."
            Copy-Item -Path $editorConfig -Destination "$module\.editorconfig" -Force
        }
    }
}

Export-ModuleMember Get-Dependencies

function Copy-BinariesFromCurrentModule {
    if ([string]::IsNullOrEmpty($global:ShellContext.CurrentModulePath)) {
        Write-Warning "The current module is not set so the binaries will not be copied"
    } else {
        Push-Location -Path $global:ShellContext.BuildScriptsDirectory
        ResolveAndCopyUniqueBinModuleContent -modulePath $ShellContext.CurrentModulePath -copyToDirectory $ShellContext.BranchServerDirectory -suppressUniqueCheck $true
        Pop-Location
    }
}

Export-ModuleMember Copy-BinariesFromCurrentModule