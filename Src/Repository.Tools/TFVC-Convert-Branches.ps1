param (
    [Parameter(Mandatory=$false, Position=0)][string]$moduleName,
    [Parameter(Mandatory=$false)][string]$tfsDirectory = "C:\TFS\ExpertSuite",
    [Parameter(Mandatory=$false)][string]$branchName = "Dev/vnext",
    [Parameter(Mandatory=$false)][string]$stagingDirectory = "C:\Temp\Staging",
    [Parameter(Mandatory=$false)][int]$changeSet,
    [switch]$restoreBranches,
    [switch]$listBranches,
    [switch]$skipBranchManipulation
)

begin {
    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'

    [string]$tfsUrl = "http://tfs.$($Env:USERDNSDOMAIN.ToLowerInvariant()):8080/tfs/"

    function Restore-Folders {
        param (
            [Parameter(Mandatory=$true)][string[]]$branches
        )

        foreach ($branch in $branches) {
            try {
                Write-Output 'Y' | TFPT.exe branches /convertToFolder "$/ExpertSuite/$branch/Modules" /collection:$tfsUrl
                Start-Sleep -Milliseconds 300 # TFPT does not support prompt suppression.
            } catch {
            }
        }
    }

    function Restore-Branches {
        param (
            [Parameter(Mandatory=$true)][System.Collections.ArrayList]$branches,
            [Parameter(Mandatory=$false)][string]$moduleName
        )

        if (-not [string]::IsNullOrWhiteSpace($moduleName)) {
            foreach ($branch in $branches) {
                try {
                    Write-Output 'Y' | TFPT.EXE branches /convertToFolder "$/ExpertSuite/$branch/Modules/$moduleName" /collection:$tfsUrl
                    Start-Sleep -Milliseconds 300 # TFPT does not support prompt suppression.
                } catch {
                }
            }
        }
    
        foreach ($branch in $branches) {
            try {
                Write-Output 'Y' | TFPT.EXE branches /convertToBranch "$/ExpertSuite/$branch/Modules" /recursive /collection:$tfsUrl
                Start-Sleep -Milliseconds 300 # TFPT does not support prompt suppression.
            } catch {
            }
        }
    }
}

process {
    if ($listBranches.IsPresent) {
        [System.Collections.ArrayList]$rootBranches = [System.Collections.ArrayList]::new()

        [string[]]$results = TFPT.EXE Branches /listBranches:roots

        for ([int]$i = 1; $i -lt $results.Count; $i++) {
            if ($results[$i].IndexOf('$/ExpertSuite') -ne -1) {
                $rootBranches.Add($results[$i].TrimStart()) | Out-Null
            }
        }

        if ($rootBranches.Count -eq 0) {
            Write-Output "No TFVC root branches for ExpertSuite found."
            exit 0
        }

        Write-Output "TFVC root branches:"
        foreach ($branch in $rootBranches) {
            Write-Output "`t$branch"
        }
        
        exit 0
    }

    [System.Collections.ArrayList]$branches = [System.Collections.ArrayList]::new()
    $branches.Add('Main') | Out-Null
    [System.Collections.ArrayList]$deletedBranches = [System.Collections.ArrayList]::new()

    [string[]]$searchPaths = @('Dev', 'Releases')

    foreach ($searchPath in $searchPaths) {
        [string[]]$results = TF.exe vc dir "$/ExpertSuite/$searchPath" /deleted

        for ([int]$i = 1; $i -lt $results.Length - 2; $i++) {
            if ($results[$i].IndexOf(';') -ne -1) {
                $deletedBranches.Add("$searchPath/$($results[$i].TrimStart().Replace('$', '').Split(';')[0])") | Out-Null
            } else {
                $branches.Add("$searchPath/$($results[$i].TrimStart().Replace('$', ''))") | Out-Null
            }
        }
    }

    Push-Location -Path $tfsDirectory

    if ($restoreBranches.IsPresent) {
        Restore-Branches -branches $branches -moduleName $moduleName
        exit 0
    }

    if (-not $skipBranchManipulation.IsPresent) {
        Restore-Folders -branches $branches
        Restore-Folders -branches $deletedBranches

        foreach ($existingBranch In TFPT.EXE Branches /listBranches:roots) {
            $existingBranch = $existingBranch.TrimStart()

            If ($existingBranch.StartsWith("$/ExpertSuite")) {
                Write-Output 'Y' | TFPT.EXE branches /convertToFolder $existingBranch
                Start-Sleep -Milliseconds 300 # TFPT does not support prompt suppression.
            }
        }

        foreach ($branch in $branches) {
            try {
                Write-Output 'Y' | TFPT.EXE branches /convertToBranch "$/ExpertSuite/$branch/Modules/$moduleName" /recursive /collection:$tfsUrl
                Start-Sleep -Milliseconds 300 # TFPT does not support prompt suppression.
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

    if (Test-Path -Path "$stagingDirectory\.gitignore") {
        [Void]$parameters.Add("--gitignore=`"$stagingDirectory\.gitignore`"")
    }

    try {
        git-tfs.exe clone @parameters
    } finally {
        Restore-Branches -branches $branches -moduleName $moduleName
    }

    Pop-Location
}