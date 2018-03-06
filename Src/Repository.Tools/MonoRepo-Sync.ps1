# Metatool for combining the multiple repositories that the ExpertSuite uses.
param (
    [Parameter(Mandatory=$false)][array]$repos=@("AccountsPayable", "AderantExpertLauncher", "Billing", "Budgeting", "Build.Infrastructure", "Case", "ClientMatter", "Conflicts", "Customization", "Database", "Deployment", "Disbursements", "Framework", "Inquiries", "MatterPlanning", "Presentation", "UIAutomation.Framework"),
    [Parameter(Mandatory=$false)][string]$branch="master"
)

if (-not (Test-Path -Path "./.git")) {
    git init
    git commit --allow-empty -m "Initial commit"
}

$baseUrl = "http://tfs.ap.aderant.com:8080/tfs/ADERANT/ExpertSuite/_git"
$exclude = $repos
$exclude += "merge.ps1"

foreach ($repo in $repos){

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