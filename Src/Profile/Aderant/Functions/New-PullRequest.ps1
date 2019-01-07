function New-PullRequest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$sourceModule = $global:ShellContext.CurrentModuleName,
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()]$targetBranch = 'master'
    )

    Set-StrictMode -Version 'Latest'

    [string]$currentBranch = git rev-parse --abbrev-ref HEAD
    [string]$repository = git ls-remote --get-url

    if ($null -ne (git ls-remote --heads $repository $currentBranch)) {
        [string]$url = "http://tfs:8080/tfs/ADERANT/ExpertSuite/_git/$($sourceModule)/pullrequestcreate?sourceRef=$($currentBranch)&targetRef=$targetBranch"
        Start-Process $url
    } else {
        Write-Error "No remote branch present. Use git push -u origin $($currentBranch)"
    }
}

Set-Alias -Name 'npr' -Value 'New-PullRequest'
Export-ModuleMember -Function 'New-PullRequest' -Alias 'npr'