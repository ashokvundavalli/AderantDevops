<#
.Synopsis
    Cleans the files from the web modules which are not in the source control.
.Description
    Following files will be deleted in the module:
        Dependencies\*
        Bin\*
        Src\$ModuleName\bin\*
    Also other files could be removed if scorch flag is on.
.PARAMETER ModuleNames
    An array of module names for which you want to clean.
.PARAMETER Scorch
    Use this switch if you want to do a scorch as well.
.EXAMPLE
    Clean Web.Presentation, Web.Foundation -Scorch
#>
function Clean($moduleNames = $ShellContext.CurrentModuleName, [switch]$scorch) {
    begin {
        function tryToRemove ($path) {
            if (Test-Path $path) {
                Remove-Item $path -recurse -Force;
            }
        }
    }

    process {
        foreach ($moduleName in $moduleNames) {
            $path = Join-Path $ShellContext.BranchLocalDirectory "Modules\$ModuleName"

            tryToRemove $path\Dependencies\*
            tryToRemove $path\Bin\*
            tryToRemove $path\Src\$ModuleName\bin\*
        }
        if ($Scorch) {
            Scorch $moduleNames;
        }
    }
}