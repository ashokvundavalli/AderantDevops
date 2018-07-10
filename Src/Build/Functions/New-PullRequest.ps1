function New-PullRequest
{
    param()      

    Set-StrictMode -Version 'Latest'

    [string]$currentBranch = git rev-parse --abbrev-ref HEAD
    [string]$repository = git ls-remote --get-url

    if ((git ls-remote --heads $repository $currentBranch) -ne $null) {
        [string]$url = "http://tfs:8080/tfs/ADERANT/ExpertSuite/_git/$($global:CurrentModuleName)/pullrequestcreate?sourceRef=$($currentBranch)&targetRef=master"
        Start-Process $url
    } else {
        Write-Error "No remote branch present. Use git push -u origin $($currentBranch)"
    }
}