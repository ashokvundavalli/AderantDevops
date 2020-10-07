function Copy-BinariesFromCurrentModule {
    if ([string]::IsNullOrEmpty($global:ShellContext.CurrentModulePath)) {
        Write-Warning "The current module is not set so the binaries will not be copied."
    } else {
        try {
            Push-Location -Path $global:ShellContext.BuildScriptsDirectory
            ResolveAndCopyUniqueBinModuleContent -modulePath $ShellContext.CurrentModulePath -copyToDirectory $ShellContext.BranchServerDirectory -suppressUniqueCheck:$true
        } finally {
            Pop-Location
        }
    }
}