cd $PSScriptRoot

& git init

Add-Content -Path ".gitignore" -value @"
[Bb]in/
[Oo]bj/
"@

& git add .
& git commit -m "Added all files"
