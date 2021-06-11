param(
    [string]$Repository
)

#=================================================================================================
# Synopsis: Performs a incremental build of the Visual Studio Solution if possible.
# Applies a common build number, executes unit tests and packages the assemblies as a NuGet
# package.
#=================================================================================================
try {
    . "$PSScriptRoot\Functions\Initialize-BuildEnvironment.ps1"

    # Import extensibility functions
    Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath 'Functions') -Filter '*.ps1' | ForEach-Object { . $_.FullName }

    Invoke-Build2 -ModulePath $Env:BUILD_SOURCESDIRECTORY -GetDependencies
} catch [Exception] {
    $Error[0] | Format-List -Force
    throw
}