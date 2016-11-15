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

		$paket = [System.IO.Path]::Combine($analyzerPaketTemplateFilePath, "..\..\", "Build", "paket.exe")
        Set-Location $analyzerPaketTemplateFilePath

        Invoke-Expression "& '$paket' --% init"

        $versionResult = Invoke-Expression "& '$paket' --% find-package-versions source http://packages.ap.aderant.com/packages/nuget name Aderant.Build.Analyzer max 1"
        $currentVersion = $versionResult[$versionResult.Count - 2]
        $paketTemplateFile = Join-Path $analyzerPaketTemplateFilePath -ChildPath "paket.template"
        $versionLine = Get-Content $paketTemplateFile | Where { $_.ToString().StartsWith("Version ") }
        $newVersion = $versionLine.Substring(8)  
              
        if ($currentVersion -ne $newVersion) {
            Write-Host "Packaging and pushing new version $newVersion of Aderant Analyzer."
            $outputPath = Join-Path $analyzerPaketTemplateFilePath -ChildPath "nugets"
            $package = Invoke-Expression "& '$paket' --% pack output $outputPath"
            $nupkgFile = (Get-ChildItem -Path $outputPath).FullName
            Invoke-Expression "& '$paket' --% push file $nupkgFile url http://packages.ap.aderant.com/packages/ apikey `" `""
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
