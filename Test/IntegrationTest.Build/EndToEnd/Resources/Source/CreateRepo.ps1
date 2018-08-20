cd $PSScriptRoot

& git init

Add-Content -Path ".gitignore" -value @"
[Bb]in/
[Oo]bj/
"@

& git add .
& git add ModuleA/Build/TFSBuild.rsp -f
& git add ModuleB/Build/TFSBuild.rsp -f
& git commit -m "Added all files"
