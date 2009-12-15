###
# Runs tests based on a name filter in the directory given
# 
# > .\RunTests.ps1 -testBinariesDirectory C:\Temp -testType IntegrationTest.Framework.Configuration
#
#     - Runs all tests in the assembly IntegrationTest.Framework.Configuration that exists in the testBinariesDirectory
#
# > .\RunTests.ps1 -testBinariesDirectory C:\Temp -testType Unit
#
#     - Runs all tests in any assemblies named UnitTest that exists in the testBinariesDirectory
###
param([string]$testBinariesDirectory, [string]$testNameFilter)

begin{
    ..\Build.\SetVisualStudioVars2010.ps1
    [void][System.Reflection.Assembly]::Load('Microsoft.Build.Utilities.v3.5, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a')    
    $MSBuild = [Microsoft.Build.Utilities.ToolLocationHelper]::GetPathToDotNetFrameworkFile("msbuild.exe", "VersionLatest")              
    
    Function RemoveExistingResultsFile([string]$testBinariesDirectory){
        Get-ChildItem -Path $testBinariesDirectory -Filter *$testNameFilter*.trx |
        ForEach-Object { Remove-Item $_.FullName -Force}
    }
    
    Function TestTypeFilter([string]$testNameFilter){
        if($testNameFilter.ToLower().Contains("unit")){
            return "Unit"
        }elseif($testNameFilter.ToLower().Contains("integration")){ 
            return "Integration"
        }
    }
                          
}

process{             

    write "Running $testType tests"
    RemoveExistingResultsFile $testBinariesDirectory
    $testType = TestTypeFilter($testNameFilter)
     
    &$MSBuild  .\RunTests.proj /p:TestBinariesDirectory=$testBinariesDirectory /p:TestType=$testType /p:TestFilter=$testNameFilter
}