# This file specifies the default launch actions from Visual Studio for this project
# Used as a debugging aid as the number of args to pass to sucessfully boot up the build system can
# be quite overwhelming

cd C:\Source\ExpertSuite\; cm .; bm  -Downstream -Transitive /p:VisualStudioVersion=14.0
#Set-PSDebug -Trace 1
$DebugPreference = 'Continue'
#C:\Source\Build.Infrastructure\Src\Package\GetProduct.ps1 -productManifestPath C:\Source\ExpertSuite\ExpertManifest.xml -dropRoot \\dfs.aderant.com\ExpertSuite\dev\vnext -binariesDirectory C:\Source\ExpertSuite\_as\_product -getDebugFiles:1