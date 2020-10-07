function global:New-PullRequest {
    [CmdletBinding()]
    [Alias('npr')]
    param(
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()]$targetBranch = 'master'
    )

    begin {
        Set-StrictMode -Version 'Latest'
    }

    process {
        [string]$sourceModule = Split-Path -Leaf (git remote get-url origin)
        [string]$currentBranch = git rev-parse --abbrev-ref HEAD
        [string]$repository = git ls-remote --get-url

        if ($null -ne (git ls-remote --heads $repository $currentBranch)) {
            [string]$url = "https://tfs.aderant.com/tfs/ADERANT/ExpertSuite/_git/$($sourceModule)/pullrequestcreate?sourceRef=$($currentBranch)&targetRef=$targetBranch"
            Start-Process $url
        } else {
            Write-Error "No remote branch present. Use git push -u origin $($currentBranch)"
        }
    }
}