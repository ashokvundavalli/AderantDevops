[string]$exe = (Join-Path (Get-BinariesPath)  \GetPackages.exe)
[string]$localModules = (Get-LocalModulesRootPath)
[string]$packages = (Join-Path (Get-LocalModulesRootPath)  \Packages)
[string]$root = "/ROOT:" + $localModules
[string]$pack = "/PACK:" + $packages
&$exe $root $pack
$curMod = (cm?)
cm Packages
cb
cm $curMod