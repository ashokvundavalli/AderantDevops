Set-StrictMode -Version 'Latest'

# PowerShell has many quirks with square brackets in paths
[string]$cwd = Get-Location
$master = ([Management.Automation.WildcardPattern]::Unescape($cwd + '.\master.txt'))
$saturn = ([Management.Automation.WildcardPattern]::Unescape($cwd + '.\saturn.txt'))
$attributes = ([Management.Automation.WildcardPattern]::Unescape($cwd + '.\.gitattributes'))

& git init .
Add-Content -LiteralPath $attributes -Value '* -text' -Force
& git add '.gitattributes'
& git commit -m 'Added git attributes'

Add-Content -LiteralPath $master -Value 'Some text' -Force

& git add 'master.txt'
& git commit -m 'Added master.txt'

# Create saturn branch
& git checkout -b 'saturn' -q
Add-Content -LiteralPath $saturn -Value 'Some text' -Force
& git add 'saturn.txt'
& git commit -m 'Added saturn.txt'