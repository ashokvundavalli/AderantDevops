function Get-ExpertModulesInChangeset {
    return $global:Workspace.GetModulesWithPendingChanges($ShellContext.BranchModulesDirectory)
}

function Get-LatestSourceForModule {
    param([string[]] $moduleNames, [string] $branchPath)

    foreach ($moduleName in $moduleNames) {
        write "*** Getting latest for $moduleName ****"
        $path = "$branchPath\Modules\$moduleName"
        Invoke-Expression "tf get $path /recursive"
    }
}