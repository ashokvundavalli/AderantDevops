Set-StrictMode -Version 'Latest'
[int]$i = 1

# PowerShell has many quirks with square brackets in paths
[string]$cwd = Get-Location
$master = [Management.Automation.WildcardPattern]::Unescape($cwd + '.\master.txt')
$saturn = [Management.Automation.WildcardPattern]::Unescape($cwd + '.\saturn.txt')

& git init
Add-Content -LiteralPath $master -Value 'Some text' -Force
& git add 'master.txt'
& git commit -m "$($i++;$i) Added master.txt"

Add-Content -LiteralPath $master -Value 'Some more'
& git add 'master.txt'
& git commit -m "$($i++;$i) Modified master.txt"

Add-Content -LiteralPath $master -Value 'Some more!'
& git add 'master.txt'
& git commit -m "$($i++;$i) Modified master.txt"

# Create saturn branch
& git checkout -b 'saturn' -q
Add-Content -LiteralPath $saturn -Value 'Some text'
& git add 'saturn.txt'
& git commit -m "$($i++;$i) Added saturn.txt"

& git checkout 'saturn' -q
Add-Content -LiteralPath $saturn -Value 'Some more text'
& git add 'saturn.txt'
& git commit -m "$($i++;$i) Modified saturn.txt"