function Install-Pester() {
    Write-Debug "Installing Pester"

    $dir = "cmd /c rmdir `"$env:USERPROFILE\Documents\WindowsPowerShell\Modules\Pester`""
    Invoke-Expression $dir

    if ($ShellContext.BranchLocalDirectory -ne $null) {
        $expression = "cmd /c mklink /d `"$env:USERPROFILE\Documents\WindowsPowerShell\Modules\Pester`" `"$($ShellContext.BranchLocalDirectory)\Modules\Build.Infrastructure\Src\Profile\Pester\`""
        Invoke-Expression $expression
    }
}