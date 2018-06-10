<#
.Synopsis
    Open the specified directory in Exploiter.
.Description
    Will open all specified directories that exist in Explorer.exe
.PARAMETER BuildScripts
    Opens your powershell directory. (often a symlink to BuildScriptsForBranch)
.PARAMETER BuildScriptsForBranch
    Opens your build scripts in Build.Infrastructure.
.PARAMETER Binaries
    Opens your branch binaries directory.
.PARAMETER ExpertSource
    Opens your branch ExpertSource, often in your binaries directory.
.PARAMETER LocalBranch
    Opens your currently selected branch on your local disk.
.PARAMETER ServerBranch
    Opens your currently selected branch on the drop server.
.PARAMETER AllModules
    Opens the Modules directory for your currently selected branch.
.PARAMETER Module
    Opens the currently selected module's directory.
.PARAMETER ModuleBin
    Opens the bin directory for your currently selected module.
.PARAMETER ModuleDependencies
    Opens the dependency directory for your currently selected module.
.PARAMETER ExpertShare
    Opens the ExpertShare for your currently selected branch.
.PARAMETER ExpertLocal
    Opens your expert local directory, normally where the binaries for your services are stored.
.PARAMETER SharedBin
    Opens your sharedbin directory with the shared binaries for your services.
.EXAMPLE
        Open-Directory -ModuleBin -SharedBin
    Will open up both the binary directory of the selected module, and the sharedbin in ExpertLocal.
#>
function Open-Directory(
    [switch]$BuildScripts, [switch]$BuildScriptsForBranch, [switch]$Binaries, [switch]$ExpertSource, [switch]$LocalBranch, [switch]$ServerBranch, [switch]$AllModules,
    [switch]$Module, [switch]$ModuleBin, [switch]$ModuleDependencies,
    [switch]$ExpertShare, [switch]$ExpertLocal, [switch]$SharedBin,
    [string]$ModuleName) {
    
    function Explorer([string]$path, [switch]$quiet) {
        if (Test-Path $path) {
            Invoke-Expression "explorer.exe $path"
            if (-not $quiet) {
                Write-Host "Opened: $path";
            }
        } else {
            Write-Host -ForegroundColor Red -NoNewline "  Directory does not exist for:";
            Write-Host " $path";
        }
    }

    #TODO: Could add a $paths which enables the user to specify arbritrary paths.

    if (
        -not $BuildScripts -and
        -not $BuildScriptsForBranch -and
        -not $Binaries -and
        -not $ExpertSource -and
        -not $LocalBranch -and
        -not $ServerBranch -and
        -not $AllModules -and
        -not $Module -and
        -not $ModuleBin -and
        -not $ModuleDependencies -and
        -not $ExpertShare -and
        -not $ExpertLocal -and
        -not $SharedBin) {

        Write-Host -ForegroundColor Yellow "Please include at least one location.";
        Write-Host "-BuildScripts, -BuildScriptsForBranch";
        Write-Host "-Binaries, -ExpertSource, -ExpertShare, -ExpertLocal, -SharedBin";
        Write-Host "-AllModules, -Module, -ModuleBin, -ModuleDependencies";
        Write-Host "-LocalBranch, -ServerBranch";
    }

    if ($BuildScripts) {
        $path = [System.IO.Path]::Combine("C:\Users\", [Environment]::UserName);
        $path = [System.IO.Path]::Combine($path, "Documents\WindowsPowerShell");
        Explorer($path);
    }
    if ($BuildScriptsForBranch) {
        Explorer($global:BuildScriptsDirectory);
    }
    if ($Binaries) {
        #product bin
        Explorer("$global:BranchBinariesDirectory");
    }
    if ($ExpertSource) {
        Explorer("$global:BranchExpertSourceDirectory");
    }
    if ($LocalBranch) {
        Explorer("$global:BranchLocalDirectory");
    }
    if ($ServerBranch) {
        Explorer("$global:BranchServerDirectory");
    }
    if ($AllModules) {
        Explorer("$global:BranchModulesDirectory");
    }
    if ($Module -or $ModuleBin -or $ModuleDependencies) {
        if (Test-Path variable:global:CurrentModulePath) {
            if ([string]::IsNullOrWhiteSpace($ModuleName)) {
                $selectedModulePath = $global:CurrentModulePath;
            } else {
                $firstHalf = $global:CurrentModulePath.Substring(0, $global:CurrentModulePath.LastIndexOf("\"));
                $selectedModulePath = [System.IO.Path]::Combine($firstHalf, $ModuleName);
            }
            if (Test-Path variable:selectedModulePath) {
                if ($Module) {
                    Explorer("$selectedModulePath");
                }
                if ($ModuleBin) {
                    $path = [System.IO.Path]::Combine($selectedModulePath, "Bin");
                    Explorer($path);
                }
                if ($ModuleDependencies) {
                    $path = [System.IO.Path]::Combine($selectedModulePath, "Dependencies");
                    Explorer($path);
                }
            } else {
                Write-Host -ForegroundColor Yellow "You seem to have misspelled the name of the module (or it doesn't exist in the current branch)."
            }
        } else {
            Write-Host -ForegroundColor Yellow "Sorry you do not have a module selected. Please select one first."
        }
    }
    if ($ExpertShare) {
        # C:\ExpertShare
        Explorer(Get-EnvironmentFromXml "/environment/@networkSharePath");

    }
    if ($ExpertLocal) {
        # C:\AderantExpert\Local
        Explorer(Get-EnvironmentFromXml "/environment/servers/server/@expertPath")
    }
    if ($SharedBin) {
        # C:\AderantExpert\Local\SharedBin
        $expertLocalPath = Get-EnvironmentFromXml("/environment/servers/server/@expertPath");
        $path = [System.IO.Path]::Combine($expertLocalPath, "SharedBin");
        Explorer($path);
    }
    #TODO: TFS
}