<# 
.Synopsis 
    Check for wrong project references in all csproj files of the module
.Example     
    Check-ForWrongReferences -moduleName "Services.Query" -localModulesRootPath "C:\TFS\ExpertSuite\Releases\803x\Modules\Services.Query"
.Parameter $moduleName is the name of the module
.Parameter $moduleRootPath is the path to the root of the module
#> 
param([string]$moduleName, [string]$moduleRootPath)

begin{
    ###
    # Check all csproj files for wrong project references
    ###
    Function Check-References {
        param($folder, $moduleName)

        $ignoreEmptyHintPaths = $true #ignore empty hint paths for now
        $areProjectReferencesValid = $true
        $isAderantBuildAnalyzerPresent = $true
        if (Test-Path $folder) {
            foreach($dir in Get-ChildItem -Path $folder) {
                foreach ($csprojFile in Get-ChildItem -Path $dir.FullName -Filter "*.csproj") {

                    # sometimes the filter does not work properly
                    if (-not $csprojFile.Name.EndsWith(".csproj", "CurrentCultureIgnoreCase")) {
                       continue
                    }
                    
                    [string] $csprojFileContent = Get-Content $csprojFile.FullName 
                    [string] $csprojFileContentUpperCase = $csprojFileContent.ToUpperInvariant()

                    # verify Aderant.Build.Analyzer reference
                    if (-not ($moduleName.StartsWith("Build.", "CurrentCultureIgnoreCase")) `
                        -and -not ($moduleName.StartsWith("Tests.", "CurrentCultureIgnoreCase")) `
                        -and -not ($moduleName.StartsWith("Internal.", "CurrentCultureIgnoreCase")) `
                        -and -not ($moduleName.EndsWith("AddIn", "CurrentCultureIgnoreCase")) `
                        -and -not ($moduleName.Contains("Install")) `
                        -and -not ($moduleName.Contains("Marketing")) `
                        -and -not ($moduleName.Contains("Help")) `
                        -and -not ($moduleName.Contains("SoftwareFactory")) `
                        -and -not ($moduleName.Contains("Exchange")) `
                        ) {
                        if (-not $csprojFileContentUpperCase.Contains("ADERANT.BUILD.ANALYZER")) {
                            Write-Host
                            Write-Host "Missing Aderant.Build.Analyzer in $module -> $csprojFile"
                            # TODO: Hack until this is deployed via Paket
                            $isAderantBuildAnalyzerPresent = $true
                        }
                    }

                    $searchPattern = "<HintPath>..\..\Bin\".ToUpperInvariant()
                    $startIndex = 0
                    while ($csprojFileContentUpperCase.IndexOf($searchPattern, $startIndex) -ge 0) {
                        if ($areProjectReferencesValid) {
                            Write-Host
                            Write-Host "* $([System.IO.Path]::GetFileName($folder)) Errors"
                        }
                        $areProjectReferencesValid = $false
                        $startIndex = $csprojFileContentUpperCase.IndexOf($searchPattern, $startIndex)
                        $endIndex = $csprojFileContentUpperCase.IndexOf("  ", $startIndex)
                        Write-Host "  ->" $csprojFileContent.Substring($startIndex, $endIndex - $startIndex)
                        $startIndex = $endIndex
                    }
                    if ($moduleName -and $moduleName -ne "Applications.Marketing") {
                        $searchPattern2 = "<HintPath>".ToUpperInvariant()
                        $startIndex2 = 0
                        while ($csprojFileContentUpperCase.IndexOf($searchPattern2, $startIndex2) -ge 0) {
                            $startIndex2 = $csprojFileContentUpperCase.IndexOf($searchPattern2, $startIndex2)
                            $endIndex2 = $csprojFileContentUpperCase.IndexOf("  ", $startIndex2)
                            $hintPath = $csprojFileContent.Substring($startIndex2, $endIndex2 - $startIndex2)
                            if (-not ($hintPath.StartsWith("<HintPath>..\..\Dependencies", "CurrentCultureIgnoreCase")) `
                                -and -not ($hintPath.StartsWith("<HintPath>..\Dependencies", "CurrentCultureIgnoreCase")) `
                                -and -not ($hintPath.StartsWith($searchPattern, "CurrentCultureIgnoreCase")) `
                                -and -not ($hintPath.StartsWith("<HintPath>$", "CurrentCultureIgnoreCase")) `
                                -and -not ($hintPath.StartsWith("<HintPath>..\..\Src\Build\Tasks", "CurrentCultureIgnoreCase")) `
                                -and -not ($hintPath.StartsWith("<HintPath>..\..\Build\Tasks", "CurrentCultureIgnoreCase")) `
                                -and -not ($hintPath.StartsWith("<HintPath>..\..\Src\Build.Tools\", "CurrentCultureIgnoreCase")) `
                                -and -not ($hintPath.StartsWith("<HintPath>..\..\Build.Tools\", "CurrentCultureIgnoreCase")) `
                                -and -not ($hintPath.StartsWith("<HintPath>..\..\packages\", "CurrentCultureIgnoreCase")) `
                                -and -not ($hintPath.StartsWith("<HintPath>..\..\..\Web.", "CurrentCultureIgnoreCase")) `
                                -and -not ($hintPath.StartsWith("<HintPath>..\..\..\..\ThirdParty\Thirdparty.Microsoft\bin", "CurrentCultureIgnoreCase") -and $module.Name -eq "Build.Infrastructure")) {
                                if (-not ($ignoreEmptyHintPaths -and $hintPath -eq "<HintPath>")) {
                                    if (-not $areProjectReferencesSuspicious) {
                                        Write-Host
                                        Write-Host "* $([System.IO.Path]::GetFileName($folder)) Warnings"
                                    }
                                    $areProjectReferencesSuspicious = $true
                                    Write-Host "  ->" $csprojFile "->" $csprojFileContent.Substring($startIndex2, $endIndex2 - $startIndex2)
                                }
                            }
                            $startIndex2++
                        }
                    }
                    $searchPattern3 = "Version=99.99.99.99"
                    $startIndex3 = 0
                    while ($csprojFileContent.IndexOf($searchPattern3, $startIndex3) -ge 0) {
                        $areProjectReferencesSuspicious = $true
                        $startIndex3 = $csprojFileContent.IndexOf($searchPattern3, $startIndex3)
                        $endIndex3 = $csprojFileContent.IndexOf("  ", $startIndex3)
                        $reference = $csprojFileContent.Substring($startIndex3, $endIndex3 - $startIndex3)
                        Write-Host $module "->" $csprojFile "->" $reference
                        $startIndex3++
                    }
                    $searchPattern4 = "System.Management.Automation, Version=1.0.0.0"
                    $startIndex4 = 0
                    while ($csprojFileContent.IndexOf($searchPattern4, $startIndex4) -ge 0) {
                        $areProjectReferencesSuspicious = $true
                        $startIndex4 = $csprojFileContent.IndexOf($searchPattern4, $startIndex4)
                        $endIndex4 = $csprojFileContent.IndexOf("  ", $startIndex4)
                        $reference = $csprojFileContent.Substring($startIndex4, $endIndex4 - $startIndex4)
                        Write-Host $module "->" $csprojFile "->" $reference
                        $startIndex4++
                    }
                }
            }
        }
        if ($isAderantBuildAnalyzerPresent -eq $false) {
            return 4
        }
        if ($areProjectReferencesValid -and -not $areProjectReferencesSuspicious) {
            return 0
        }
        if (-not $areProjectReferencesValid -and -not $areProjectReferencesSuspicious) {
            return 1
        }
        if ($areProjectReferencesValid -and $areProjectReferencesSuspicious) {
            return 2
        }
        if (-not $areProjectReferencesValid -and $areProjectReferencesSuspicious) {
            return 3
        }
    }  
}

process{
    $areProjectReferencesValid = $true

    $srcFolder = Join-Path $moduleRootPath "Src"
    $srcCheckResult = Check-References -folder $srcFolder -moduleName $moduleName

    $testFolder = Join-Path $moduleRootPath "Test"
    $testCheckResult = Check-References -folder $testFolder -moduleName $moduleName

    if ($srcCheckResult -eq 1 -or $srcCheckResult -eq 3 -or $testCheckResult -eq 1 -or $testCheckResult -eq 3) {
        Write-Host
        Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
        Write-Host "One or more references are not set up properly for multi-core builds in module" $moduleName "- references to projects within the same solution have to be set as project references, all other references should point to the Dependencies folder (never point to the build output folder)."
        Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
        Write-Host
        throw "Invalid project references"
    } elseif ($srcCheckResult -eq 2 -or $testCheckResult -eq 2) {
        Write-Host
        Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
        Write-Host "One or more references might not be set up properly - references to projects within the same solution have to be set as project references, all other references should point to the Dependencies folder."
        Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
        Write-Host
        throw "Invalid project references"

        #if ($Env:TF_BUILD -eq $null) {
        #    Write-Host "Continuing build in 10 seconds..."
        #    Start-Sleep -s 10
        #}
    } elseif ($srcCheckResult -eq 4 -or $testCheckResult -eq 4) {
        Write-Host
        Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
        Write-Host "One or more projects don't have the Aderant.Build.Analyzer set as an analyzer reference - add the analyzer reference to Aderant.Build.Analyzer located as follows:"
        Write-Host '  <ItemGroup Condition="''$(BuildToolsDirectory)'' == ''''">'
        Write-Host '    <Analyzer Include="..\..\..\Build.Infrastructure\Src\Build.Tools\Aderant.Build.Analyzer.dll" />'
        Write-Host '  </ItemGroup>'
        Write-Host '  <ItemGroup Condition="''$(BuildToolsDirectory)'' != ''''">'
        Write-Host '    <Analyzer Include="$(BuildToolsDirectory)\Aderant.Build.Analyzer.dll" />'
        Write-Host '  </ItemGroup>"'
        Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
        Write-Host
        throw "Missing Aderant.Build.Analyzer references"
    } elseif ($srcCheckResult -eq 0 -or $testCheckResult -eq 0) {
        Write-Host
        Write-Host "All references seem to be set up right."
        Write-Host
    } 
}
