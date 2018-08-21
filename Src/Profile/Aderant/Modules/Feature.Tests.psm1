<#
.SYNOPSIS
Runs UI tests for the current module
.PARAMETER productname
    The name of the product you want to run tests against
.PARAMETER testCaseFilter
    The vstest testcasefilter string to use
.PARAMETER dockerHost
    The dockerHost to run the docker container on
.EXAMPLE
    Run-ExpertUITest -productname "Web.Inquiries" -testCaseFilter "TestCategory=Smoke"
    If Inquiries is the current module, all smoke tests for the inquiries product will be executed
#>
function Run-ExpertUITests {
    param(
        [Parameter(Mandatory = $false)] [string]$productName = "*",
        [Parameter(Mandatory = $false)] [string]$testCaseFilter = "TestCategory=Sanity",
        [Parameter(Mandatory = $false)] [string]$dockerHost = "",
        [Parameter(Mandatory = $false)] [string]$browserName,
        [Parameter(Mandatory = $false)] [switch]$deployment,
        [Parameter(Mandatory = $false)] [switch]$noBuild,
        [Parameter(Mandatory = $false)] [switch]$noDocker
    )
    if (-Not $CurrentModuleName) {
        Write-Error "You must select a module to run this command"
        Break
    }
    if ([string]::IsNullOrWhiteSpace($dockerHost) -and -Not (Get-Command docker -errorAction SilentlyContinue)) {
        Write-Error "Docker not installed. Please install Docker for Windows before running this command"
        Break
    }
    if ($productName -eq "*") {
        Write-Host "No project name specified. Running sanity test for all test containers"
    }

    $testOutputPath = "$currentModulePath\Bin\Test\"
    $testAssemblyName = "UIAutomationTest.*$productName*.dll"
    $testAssemblyPath = $testOutputPath + $testAssemblyName
    $runsettingsFile = $testOutputPath + "localexecution.runsettings"
    $runSettings

    if (!$deployment -or $noDocker -or $browserName -or ![string]::IsNullOrWhiteSpace($dockerHost)) {
        $runSettingsParameters
        if ($browserName) {
            $runSettingsParameters += '<Parameter name="BrowserName" value="' + $browserName + '" />
            '
        }
        if (!$deployment) {
            $runSettingsParameters += '<Parameter name="Development" value="true" />
            '
        }
        if ($noDocker) {
            Get-ChildItem "$CurrentModulePath\Packages\**\InitializeSeleniumServer.ps1" -Recurse | Import-Module
            Start-SeleniumServers
            $runSettingsParameters += '<Parameter name="NoDocker" value="true" />
            '
        }
        if (![string]::IsNullOrWhiteSpace($dockerHost)) {
            $runSettingsParameters += '<Parameter name="DockerHost" value="' + $dockerHost + '" />
            '
        }

        $runSettingsContent = '<?xml version="1.0" encoding="utf-8"?>  
        <RunSettings>
            <!-- Parameters used by tests at runtime -->  
            <TestRunParameters>
            ' + $runSettingsParameters +
        '</TestRunParameters>
        </RunSettings>'
        if (Test-Path $runsettingsFile) {
            Remove-Item $runsettingsFile
        }
        Write-Host "Creating custom runsettings file"
        New-Item -Path $runsettingsFile -ItemType "file" -Value $runSettingsContent > $null
        $runSettings = "/Settings:$runsettingsFile"
    }

    if (-Not $noBuild) {
        Get-ChildItem -Path ("$currentModulePath\Test\*$productName*\") -Recurse -Filter "UIAutomationTest.*.csproj" | ForEach-Object {$_} {
            msbuild $_
        }
    }

    if (-Not (Test-Path($testAssemblyPath))) {
        Write-Error "Cannot find $testAssemblyPath. Please ensure this module has been built before trying to execute tests"
        Break
    }
    $testAssemblies = Get-ChildItem -Path $testAssemblyPath -Recurse -Filter $testAssemblyName
    vstest.console.exe $testAssemblies /TestCaseFilter:$testCaseFilter /TestAdapterPath:$testOutputPath /Logger:trx $runSettings
}

Export-ModuleMember -Function Run-ExpertUITests

<#
.SYNOPSIS
    Runs UI sanity tests for the current module
.PARAMETER productName
    The name of the product you want to run tests against
.EXAMPLE
    Run-ExpertSanityTests -productName "Web.Inquiries"
    rest "Web.Inquiries" -deployment
    This will run the UI sanity tests for the Inquiries product against a deployment url
#>
function Run-ExpertSanityTests {
    param(
        [Parameter(Mandatory=$false)] [string]$productName = "*",
        [Parameter(Mandatory=$false)] [string]$dockerHost = "",
        [Parameter(Mandatory=$false)] [string]$browserName,
        [Parameter(Mandatory=$false)] [switch]$deployment,
        [Parameter(Mandatory=$false)] [switch]$noDocker

    )
    Run-ExpertUITests -productName $productName -testCaseFilter "TestCategory=Sanity" -dockerHost:$dockerHost -deployment:$deployment -noDocker:$noDocker -browserName $browserName
}

Export-ModuleMember -Function Run-ExpertSanityTests

<#
.SYNOPSIS
    Runs UI visual tests for the current module
.PARAMETER productName
    The name of the product you want to run tests against
.PARAMETER development
    Run against the developer environment
.PARAMETER noDocker
    Don't use docker
.PARAMETER browserName
    Browser to use for the test
.EXAMPLE
    Run-ExpertVisualTests -productName "Web.Inquiries"
    rest "Web.Inquiries" -deployment
    This will run the UI visual tests for the Inquiries product against a deployment url
#>
function Run-ExpertVisualTests {
    param(
        [Parameter(Mandatory = $false)] [string]$productName = "*",
        [Parameter(Mandatory = $false)] [string]$dockerHost = "",
        [Parameter(Mandatory = $false)] [string]$browserName,
        [Parameter(Mandatory = $false)] [switch]$deployment,
        [Parameter(Mandatory = $false)] [switch]$noDocker

    )
    Run-ExpertUITests -productName $productName -testCaseFilter "TestCategory=Visual" -dockerHost:$dockerHost -deployment:$deployment -noDocker:$noDocker -browserName $browserName
}

Export-ModuleMember -Function Run-ExpertVisualTests