Set-StrictMode -Version 'Latest'

# This module provides a function to copy missing files to the 'Src\MyProject\bin' directory for web projects.
# This is required to facilitate the debugging experience for web development.
# The files that get copied across by this script would be resolved the regular way by the AssemblyResolver for a deployed Expert environment.

[System.Collections.ArrayList]$script:ToBeCopiedList =
                    "Factory.bin",
                    "Aderant.Notification.Service.dll",
                    "Aderant.StoredProcedures.Core.dll",
                    "Aderant.*.StoredProcedures.dll"

$script:SourceFolder = "C:\AderantExpert\Local\SharedBin\"

function global:Get-CmdsConfig {
    Write-Host "List of files to be copied from '"$SourceFolder"':"

    foreach ($file in $script:ToBeCopiedList) {
        Write-Host $file
    }
}

function global:Copy-MissingDlls {
    if ([string]::IsNullOrEmpty($Global:CurrentModulePath)) {
        Write-Host "No module seleced."
        return
    }

    $webProjects = Get-ChildItem "$Global:CurrentModulePath\Src\Web.*" -Directory
    
    foreach ($project in $webProjects) {
        $dest = "$project\bin"

        if (!(Test-Path $dest)) {
            Write-Error "Folder does not exist: $dest"
            continue
        }

        Copy-Item "$($script:SourceFolder)\*" -Destination $dest -Force -Include $script:ToBeCopiedList
        Write-Host "Copied from $($script:SourceFolder) to $dest"
    }
}

Set-Alias -Name cmds -Value Copy-MissingDlls -Scope global

Export-ModuleMember -Function *