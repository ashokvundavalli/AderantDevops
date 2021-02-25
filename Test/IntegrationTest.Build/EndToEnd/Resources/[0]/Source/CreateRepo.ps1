Set-StrictMode -Version 'Latest'

& git init .

[string]$cwd = Get-Location

$gitattributesFile = $cwd + '\.gitattributes'

[System.IO.File]::WriteAllLines($gitattributesFile, '* -text')

& git add '.gitattributes'
& git commit -m 'Added git attributes'

$file = $cwd + '\.gitignore'

[System.IO.File]::WriteAllLines($file, @'
[Bb]in/
[Oo]bj/
'@)

& git add '-A' '-f'
& git add 'ModuleA\Build\TFSBuild.rsp' '-f'
& git add 'ModuleB\Build\TFSBuild.rsp' '-f'
& git commit -m 'Added all files'