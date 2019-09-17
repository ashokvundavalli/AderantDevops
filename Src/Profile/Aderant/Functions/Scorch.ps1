<#
.Synopsis
    Scorch the given modules.
.PARAMETER ModuleNames
    An array of module names for which you want to scorch.
.EXAMPLE
    Scorch Web.Presentation, Web.Foundation
#>
function Scorch($moduleNames = $global:ShellContext.CurrentModuleName) {
    foreach ($moduleName in $moduleNames) {
        $path = Join-Path $global:ShellContext.BranchLocalDirectory "Modules\$ModuleName"
        invoke-expression "tfpt scorch $path /recursive /noprompt";
    }
}