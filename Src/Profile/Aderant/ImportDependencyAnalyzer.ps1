#Imports the DependencyAnalyzer.dll without locking it on disk
$scriptPath = split-path -parent $MyInvocation.MyCommand.Definition
$file = "$scriptPath\DependencyAnalyzer.dll"
$assemblyBytes = [System.IO.File]::ReadAllBytes($file)

[System.Reflection.Assembly]$assembly = [System.Reflection.Assembly]::Load($assemblyBytes)
Import-Module $assembly
