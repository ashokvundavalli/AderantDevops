<#
.Synopsis
    Gets the latest source from TFS
.Description
    Get the latest source from TFS for a module, or if no module is specified, the current module, or if -Branch is specified, the entire branch
.PARAMETER ModuleName
    The name of the module
.PARAMETER Branch
    Gets latest for the entire branch instead of a particular module
.EXAMPLE
        Get-Latest Libraries.Presentation
    Gets the latest source for Libraries.Presentation from TFS
.EXAMPLE
        Get-Latest
    Gets the latest source for the current module from TFS
.EXAMPLE
        Get-Latest -Branch
    Gets the latest source for the current branch from TFS
#>
function Get-Latest([string] $ModuleName, [switch] $Branch) {
    $sourcePath = $null;
    if ($Branch) {
        $sourcePath = $ShellContext.BranchLocalDirectory
    } else {
        if (!($ModuleName)) {
            $ModuleName = $ShellContext.CurrentModuleName
        }
        if (!($ModuleName) -or $ModuleName -eq $null -or $ModuleName -eq "") {
            "No module specified"
            return
        }
        $sourcePath = Join-Path $ShellContext.BranchLocalDirectory "Modules\$ModuleName"
        if ((Test-Path $sourcePath) -eq $False) {
            "There is no local path $sourcePath. Make sure you are specifying a module that exists in the current branch"
            return
        }
    }

    Invoke-Expression "tf get $sourcePath /recursive"
}