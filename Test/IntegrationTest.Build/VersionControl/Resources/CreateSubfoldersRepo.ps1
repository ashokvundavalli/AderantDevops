Set-StrictMode -Version 'Latest'

# PowerShell has many quirks with square brackets in paths
[string]$cwd = Get-Location
[string]$subfolder = 'Suite'

#Create root folder for a "cone"
New-Item -Name $subfolder -ItemType "directory"
& git init .
cd ($subfolder)
$cwd = Get-Location

#Create sub-directories under the cone
[string]$subSubfolder1 = 'Product1'
[string]$subSubfolder2 = 'Product2'
New-Item -Name $subSubFolder1 -ItemType "directory"
New-Item -Name $subSubfolder2 -ItemType "directory"

$file1 = ([Management.Automation.WildcardPattern]::Unescape($cwd + '\' + $subSubFolder1 + '\master1.txt'))
$file2 = ([Management.Automation.WildcardPattern]::Unescape($cwd + '\' + $subSubfolder2 + '\master2.txt'))

#Add a file under the first subfolder and commit
Add-Content -LiteralPath $file1 -Value 'Some text' -Force
& git add $file1
& git commit -m ('Added ' + $file1)

#Add a file under the second subfolder in another commit
& git checkout -b 'saturn' -q
Add-Content -LiteralPath $file2 -Value 'Some text' -Force
& git add $file2
& git commit -m ('Added ' + $file2)

cd ..\