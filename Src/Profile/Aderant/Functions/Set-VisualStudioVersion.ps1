function Set-VisualStudioVersion() {
    $file = [System.IO.Path]::Combine($ShellContext.BuildScriptsDirectory, "vsvars.ps1");
    if (Test-Path $file) {
        & $file
    }
}