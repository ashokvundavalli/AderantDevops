# This file specifies the default launch actions from Visual Studio for this project
# Used as a debugging aid as the number of args to pass to sucessfully boot up the build system can
# be quite overwhelming
#bm -downstream -transitive /p:VisualStudioVersion=14.0 /p:PackageArtifacts=true /p:T4TransformEnabled=false /p:RetrievePrebuilts=false
cd C:\Git\ExpertSuite\;cm .
bm -Include 'C:\Git\WebCore'
#bm -WhatIf -JustMyChanges -downstream -transitive #-NoBuildCache 
#bm -DirectoriesToBuild C:\tfs\ExpertSuite\Dev\vnext\Modules\SDK.Workflow\  -WhatIf


#C:\Source\Build.Infrastructure\Src\Package\GetProduct.ps1 -productManifestPath C:\Source\ExpertSuite\ExpertManifest.xml -dropRoot \\dfs.aderant.com\ExpertSuite\dev\vnext -binariesDirectory C:\Source\ExpertSuite\_as\_product -getDebugFiles:1