Set-StrictMode -Version Latest
$VerbosePreference = 'Continue'

Function DFS {
    Param([string]$Path)
    $directories = @(Get-ChildItem -Path $Path -Directory -Force -ErrorAction SilentlyContinue)
    $dirs = $directories | Where-Object { @(Get-ChildItem $_.fullName).count -eq 0 } | Select-Object -expandproperty FullName
    $dirs | Foreach-Object { Remove-Item $_ -Verbose }
    foreach($d in $directories) { DFS $d.fullname }
}

DFS('\\dfs.aderant.com\expert-ci\pulls')
DFS('\\dfs.aderant.com\expert-ci\product')
DFS('\\dfs.aderant.com\expert-ci\prebuilts\v1\')