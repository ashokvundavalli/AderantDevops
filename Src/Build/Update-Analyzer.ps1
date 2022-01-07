<#
.Synopsis
    Updates the Aderant.Analyzer package if it changed.
#>
param([string] $analyzerPaketTemplateFilePath)

begin{
    ###
    # Check if the analyzer version is up to date and update it if it's newer.
    ###
    Function CheckAndUpdate-AnalyzerVersion {
        param([string] $analyzerPaketTemplateFilePath)

        # get paket.exe location
        $paket = [System.IO.Path]::Combine($analyzerPaketTemplateFilePath, "..\..\", "Build.Tools", "paket.exe")

        Set-Location $analyzerPaketTemplateFilePath

        # initialize paket
        Invoke-Expression "& '$paket' --% init"

        # detect existing analyzer package version
        $versionResult = Invoke-Expression "& '$paket' --% find-package-versions source https://expertpackages.azurewebsites.net//packages/nuget name Aderant.Build.Analyzer max 1"
        $currentVersion = $versionResult[$versionResult.Count - 2]

        # analyze paket.template to find out if the version number increased
        $paketTemplateFile = Join-Path $analyzerPaketTemplateFilePath -ChildPath "paket.template"
        $versionLine = Get-Content $paketTemplateFile | Where { $_.ToString().StartsWith("Version ") }
        $newVersion = $versionLine.Substring(8)

        # update package if the version number changed
        if ($currentVersion -ne $newVersion) {
            Write-Host "Packaging and pushing new version $newVersion of Aderant Analyzer."
            $outputPath = Join-Path $analyzerPaketTemplateFilePath -ChildPath "nugets"

            # package it
            $package = Invoke-Expression "& '$paket' --% pack output $outputPath"
            $nupkgFile = (Get-ChildItem -Path $outputPath).FullName

            # push it
            Invoke-Expression "& '$paket' --% push file $nupkgFile url https://expertpackages.azurewebsites.net//packages/ apikey `" `""

            # clean up
            Remove-Item -Path $outputPath -Recurse
        } else {
            Write-Host "Version $currentVersion of Aderant Analyzer is already up to date."
        }
    }
}

process{
    Write-Host "Checking version of Aderant Analyzer in $analyzerPaketTemplateFilePath ..."
    $isAnalyzerUpToDate = CheckAndUpdate-AnalyzerVersion $analyzerPaketTemplateFilePath
}
