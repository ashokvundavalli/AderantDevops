# Metatool for combining the multiple repositories  that the ExpertSuite uses.
[CmdletBinding()]
param (
    [Parameter(Mandatory=$false)][string]$repositoryDirectory,
#    [Parameter(Mandatory=$false)][string[]]$repositories = @("AccountsPayable", "AderantExpertLauncher", "Billing", "Budgeting", "Build.Infrastructure", "Case", "ClientMatter", "Conflicts", "Customization", "Database", "Deployment", "Disbursements", "Framework", "Inquiries", "MatterPlanning", "Presentation", "UIAutomation.Framework"),
    [Parameter(Mandatory=$false)][string[]]$repositories = @("Deployment", "Framework", "AccountsPayable"),
    [Parameter(Mandatory=$false)][string]$branch = "master",
    [switch]$initialize
)

begin {
    Set-StrictMode -Version Latest
    $ErrorActionPreference = "Stop"

    if ([string]::IsNullOrWhiteSpace($repositoryDirectory)) {
        if ($initialize.IsPresent) {
            Write-Error "The repositoryDirectory parameter must be specified when using the -initialize switch."
            
            return
        }

        $repositoryDirectory = Convert-Path .
    }

    if ($initialize.IsPresent) {
        if (Test-Path -Path $repositoryDirectory) {
            Remove-Item -Path $repositoryDirectory -Recurse -Force
        }
    }

    if (-not (Test-Path $repositoryDirectory)) {
        New-Item -ItemType Directory -Path $repositoryDirectory | Out-Null
    }
}

process {
    if (-not (Test-Path -Path (Join-Path -Path $repositoryDirectory -ChildPath ".git"))) {
        Push-Location -Path $repositoryDirectory
        git init
        git commit --allow-empty -m "Initial commit"
    }

    $baseUrl = "http://tfs.ap.aderant.com:8080/tfs/ADERANT/ExpertSuite/_git"
    $exclude = $repositories

    foreach ($repo in $repositories) {
        if (Test-Path -Path "./$repo") {
            if ($repo -eq "Disbursements") {
                git subtree pull --prefix $repo $repo $branch --squash
                Write-Host "Pull $repo/$branch completed."
            } else {
                git subtree pull --prefix $repo $repo $branch
                Write-Host "Pull $repo/$branch completed."
            }
        } else {
            git remote add -f $repo "$baseUrl/$repo"
            Write-Host "Add remote $repo done."

            # This guy has a messed up history graph so we treat it as a special case. The history of objects from this won't have the same fidelity as the rest of the merged repositories.
            if ($repo -eq "Disbursements") {
                git subtree add --prefix $repo $repo $branch --squash
                git subtree pull --prefix $repo $repo $branch --squash
            } else {
                git subtree add --prefix $repo $repo $branch
                git subtree pull --prefix $repo $repo $branch
            }
        }
    }
}

end {
    Pop-Location
}