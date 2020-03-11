Set-StrictMode -Version 'Latest'

function LoadAssembly {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$buildAnalyzerAssembly
    )

    if (-not [System.IO.File]::Exists($buildAnalyzerAssembly)) {
        throw "Assembly: '$buildAnalyzerAssembly' does not exist."
    }

    $assembly = [System.Reflection.Assembly]::LoadFile($buildAnalyzerAssembly)
    $version = $assembly.GetName().Version;

    if ($version.Major -gt 1 -or $version.Major -eq 1 -and $version.Minor -gt 0) {
        return
    }

    throw "-SuppressDiagnostics argument was specified, but the version of 'Aderant.Build.Analyzer.dll' 
        found does not support this argument. 'Aderant.Build.Analyzer.dll' must be at least version 1.1.*. 
        Version found: '$version' 
        Path found: '$buildAnalyzerAssembly'."
}

function CleanSuppressionFiles {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$buildAnalyzerAssembly,
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$path
    )

    begin {
        LoadAssembly -buildAnalyzerAssembly $buildAnalyzerAssembly
    }

    process {
        if (-not [System.IO.Directory]::Exists($path)) {
            throw "Path: '$path' does not exist."
        }

        [Aderant.Build.Analyzer.GlobalSuppressions.GlobalSuppressionsController]::CleanFiles($path)
    }
}

function OrganizeSuppressionFiles {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$buildAnalyzerAssembly,
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$path
    )

    begin {
        LoadAssembly -buildAnalyzerAssembly $buildAnalyzerAssembly
    }

    process {
        if (-not [System.IO.Directory]::Exists($path)) {
            throw "Path: '$path' does not exist."
        }

        [Aderant.Build.Analyzer.GlobalSuppressions.GlobalSuppressionsController]::OrganizeFiles($path)
    }
}