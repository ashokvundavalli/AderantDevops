<#
.Synopsis
    Scorch the given modules.
.PARAMETER ModuleNames
    An array of module names for which you want to scorch.
.EXAMPLE
    Scorch Web.Presentation, Web.Foundation
#>
function Scorch($moduleNames = $ShellContext.CurrentModuleName) {
    foreach ($moduleName in $moduleNames) {
        $path = Join-Path $ShellContext.BranchLocalDirectory "Modules\$ModuleName"
        invoke-expression "tfpt scorch $path /recursive /noprompt";
    }
}