Set-StrictMode -Version 'Latest'

# PowerShell has many quirks with square brackets in paths
[string]$cwd = Get-Location
$branch1 = ([Management.Automation.WildcardPattern]::Unescape($cwd + '.\branch1.txt'))
$branch1a = ([Management.Automation.WildcardPattern]::Unescape($cwd + '.\branch1a.txt'))
$branch2 = ([Management.Automation.WildcardPattern]::Unescape($cwd + '.\branch2.txt'))

& git init .
Add-Content -LiteralPath $branch1 -Value 'foo' -Force

& git add .
& git commit -m 'foo'

& git checkout -b 'branch1' -q
Add-Content -LiteralPath $branch2 -Value 'bar' -Force
& git add .
& git commit -m 'bar'

& git checkout 'master' -q
Add-Content -LiteralPath $branch1 -Value 'baz' -Force
Add-Content -LiteralPath $branch1a -Value 'gaz' -Force
& git add .
& git commit -m 'baz & gaz'

& git checkout 'branch1' -q

git pull . master -q