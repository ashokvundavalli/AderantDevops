<#
.Synopsis
    cd to the specified directory.
.Description
    Will change your working directory to the specified directory. NOTE: this is similar to odir.
.PARAMETER BuildScripts
    cd to your powershell directory. (often a symlink to BuildScriptsForBranch)
.PARAMETER BuildScriptsForBranch
    cd to your build scripts in Build.Infrastructure.
.PARAMETER Binaries
    cd to your branch binaries directory.
.PARAMETER ExpertSource
    cd to your branch ExpertSource, often in your binaries directory.
.PARAMETER LocalBranch
    cd to your currently selected branch on your local disk.
.PARAMETER ServerBranch
    cd to your currently selected branch on the drop server.
.PARAMETER AllModules
    cd to the Modules directory for your currently selected branch.
.PARAMETER Module
    cd to the currently selected module's directory.
.PARAMETER ModuleBin
    cd to the bin directory for your currently selected module.
.PARAMETER ModuleDependencies
    cd to the dependency directory for your currently selected module.
.PARAMETER ExpertShare
    cd to the ExpertShare for your currently selected branch.
.PARAMETER ExpertLocal
    cd to your expert local directory, normally where the binaries for your services are stored.
.PARAMETER SharedBin
    cd to your sharedbin directory with the shared binaries for your services.
.EXAMPLE
        Change-Directory -ModuleBin
    Will cd to your currently selected module's bin directory.
#>
function Change-Directory(
    [switch]$BuildScripts, [switch]$BuildScriptsForBranch, [switch]$Binaries, [switch]$ExpertSource, [switch]$LocalBranch, [switch]$ServerBranch, [switch]$AllModules,
    [switch]$Module, [switch]$ModuleBin, [switch]$ModuleDependencies,
    [switch]$ExpertShare, [switch]$ExpertLocal, [switch]$SharedBin) {

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
        cd $path;
    }
    if ($BuildScriptsForBranch) {
        cd $global:BuildScriptsDirectory;
    }
    if ($Binaries) {
        #product bin
        cd "$global:BranchBinariesDirectory";
    }
    if ($ExpertSource) {
        cd "$global:BranchExpertSourceDirectory";
    }
    if ($LocalBranch) {
        cd "$global:BranchLocalDirectory";
    }
    if ($ServerBranch) {
        cd "$global:BranchServerDirectory";
    }
    if ($AllModules) {
        cd "$global:BranchModulesDirectory";
    }

    if ($Module) {
        if (Test-Path variable:global:CurrentModulePath) {
            cd "$global:CurrentModulePath";
        } else {
            Write-Host -ForegroundColor Yellow "Sorry you do not have a module selected."
        }
    }
    if ($ModuleBin) {
        if (Test-Path variable:global:CurrentModulePath) {
            $path = [System.IO.Path]::Combine($global:CurrentModulePath, "Bin");
            cd $path;
        } else {
            Write-Host -ForegroundColor Yellow "Sorry you do not have a module selected."
        }
    }
    if ($ModuleDependencies) {
        if (Test-Path variable:global:CurrentModulePath) {
            $path = [System.IO.Path]::Combine($global:CurrentModulePath, "Dependencies");
            cd $path;
        } else {
            Write-Host -ForegroundColor Yellow "Sorry you do not have a module selected."
        }
    }
    if ($ExpertShare) {
        # C:\ExpertShare
        $path = Get-EnvironmentFromXml "/environment/@networkSharePath";
        cd $path;
    }
    if ($ExpertLocal) {
        # C:\AderantExpert\Local
        $path = Get-EnvironmentFromXml "/environment/servers/server/@expertPath"
        cd $path;
    }
    if ($SharedBin) {
        # C:\AderantExpert\Local\SharedBin
        $expertLocalPath = Get-EnvironmentFromXml("/environment/servers/server/@expertPath");
        $path = [System.IO.Path]::Combine($expertLocalPath, "SharedBin");
        cd $path;
    }
    #TODO: TFS root.
}