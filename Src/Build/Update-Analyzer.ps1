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

        $paket = [System.IO.Path]::Combine($Env:EXPERT_BUILD_DIRECTORY, "Build", "paket.exe")
        Set-Location $analyzerPaketTemplateFilePath

        Invoke-Tool -FileName $paket -Arguments "init" -RequireExitCodeZero 

        $versionResult = Invoke-Tool -FileName $paket -Arguments "find-package-versions source http://packages.ap.aderant.com/packages/nuget name Aderant.Build.Analyzer max 1"
        $currentVersion = $versionResult[$versionResult.Count - 2]
        $paketTemplateFile = Join-Path $analyzerPaketTemplateFilePath -ChildPath "paket.template"
        $versionLine = Get-Content $paketTemplateFile | Where { $_.ToString().StartsWith("Version ") }
        $newVersion = $versionLine.Substring(8)  
              
        if ($currentVersion -ne $newVersion) {
            Write-Info "Packaging and pushing new version $newVersion of Aderant Analyzer."
            $outputPath = Join-Path $analyzerPaketTemplateFilePath -ChildPath "nugets"
            $package = Invoke-Tool -FileName $paket -Arguments "pack output $outputPath" -RequireExitCodeZero
            $nupkgFile = (Get-ChildItem -Path $outputPath).FullName
            Invoke-Tool -FileName $paket -Arguments "push file $nupkgFile url http://packages.ap.aderant.com/packages/ apikey `" `"" -RequireExitCodeZero
            Remove-Item -Path $outputPath -Recurse
        } else {
            Write-Info "Version $currentVersion of Aderant Analyzer is already up to date."
        }
    }  
}

process{
    Write-Info "Checking version of Aderant Analyzer..."
    $isAnalyzerUpToDate = CheckAndUpdate-AnalyzerVersion $analyzerPaketTemplateFilePath
}
