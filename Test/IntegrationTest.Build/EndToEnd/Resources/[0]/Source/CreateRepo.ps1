Set-StrictMode -Version 'Latest'

& git init .

[string]$cwd = Get-Location
$file = $cwd + '\.gitignore'

[System.IO.File]::WriteAllLines($file, @'
[Bb]in/
[Oo]bj/
'@)

& git add '-A' '-f'
& git add 'ModuleA\Build\TFSBuild.rsp' '-f'
& git add 'ModuleB\Build\TFSBuild.rsp' '-f'
& git commit -m 'Added all files'