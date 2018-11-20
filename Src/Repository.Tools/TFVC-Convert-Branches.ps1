param (
    [Parameter(Mandatory=$false, Position=0)][string]$moduleName,
    [Parameter(Mandatory=$false)][string]$tfsDirectory = "C:\TFS\ExpertSuite",
    [Parameter(Mandatory=$false)][string]$branchName = "Dev/vnext",
    [Parameter(Mandatory=$false)][string]$stagingDirectory = "C:\Temp\Staging",
    [Parameter(Mandatory=$false)][int]$changeSet,
    [switch]$gitIgnore,
    [switch]$restoreBranches,
    [switch]$skipBranchManipulation
)

begin {
    Set-StrictMode -Version Latest

    function Restore-Folders {
        param (
            [Parameter(Mandatory=$true)][string[]]$branches
        )

        foreach ($branch in $branches) {
            try {
                Write-Output y|tfpt branches /convertToFolder "$/ExpertSuite/$branch/Modules"
            } catch {
            }
        }
    }

    function Restore-Branches {
        param (
            [Parameter(Mandatory=$true)][string[]]$branches,
            [Parameter(Mandatory=$false)][string]$moduleName
        )

        if (-not [string]::IsNullOrWhiteSpace($moduleName)) {
            foreach ($branch in $branches) {
                try {
                    Write-Output y|tfpt branches /convertToFolder "$/ExpertSuite/$branch/Modules/$moduleName"
                } catch {
                }
            }
        }
    
        foreach ($branch in $branches) {
            try {
                Write-Output y|tfpt branches /convertToBranch "$/ExpertSuite/$branch/Modules" /recursive
            } catch {
            }
        }
    }

    Push-Location -Path $tfsDirectory

    [System.Collections.ArrayList]$branches = @('Main')
    [System.Collections.ArrayList]$deletedBranches = @()

    [string[]]$searchPaths = @('Dev', 'Releases')

    foreach ($searchPath in $searchPaths) {
        [string[]]$results = TF.exe vc dir "$/ExpertSuite/$searchPath" /deleted

        for ([int]$i = 1; $i -lt $results.Count - 2; $i++) {
            if ($results[$i].IndexOf(';')) {
                $deletedBranches.Add("$searchPath/$($results[$i].TrimStart().Replace('$', '').Split(';')[0])") | Out-Null
            } else {
                $branches.Add($results[$i].TrimStart().Replace('$', '')) | Out-Null
            }
        }
    }
}

process {
    if ($restoreBranches.IsPresent) {
        Restore-Branches -branches $branches -moduleName $moduleName
        return
    }

    if (-not $skipBranchManipulation.IsPresent) {
        Restore-Folders -branches $branches
        Restore-Folders -branches $deletedBranches

        foreach ($existingBranch In TFPT.EXE Branches /listBranches:roots) {
            $existingBranch = $existingBranch.TrimStart()

            If ($existingBranch.StartsWith("$/ExpertSuite")) {
                Write-Output y|tfpt branches /convertToFolder $($existingBranch)
            }
        }

        foreach ($branch in $branches) {
            try {
                Write-Output y|tfpt branches /convertToBranch "$/ExpertSuite/$branch/Modules/$moduleName" /recursive
            } catch {
            }
        }
    }

    Write-Output "Using staging directory: $stagingDirectory"
    Set-Location -Path $stagingDirectory

    [System.Collections.ArrayList]$parameters = @(
        "--resumable",
        "http://tfs:8080/tfs/Aderant",
        "$/ExpertSuite/$($branchName)/Modules/$moduleName",
        "$($stagingDirectory)\$($moduleName)",
        "--batch-size=50"
    )

    if ($changeSet -ne 0) {
        [Void]$parameters.Add("--changeset=$changeSet")
    }

    if ($gitIgnore.IsPresent) {
        [Void]$parameters.Add("--gitignore=`"$stagingDirectory\.gitignore`"")
    }

    git-tfs.exe clone @parameters

    Restore-Branches -branches $branches -moduleName $moduleName
}

end {
    Pop-Location
}