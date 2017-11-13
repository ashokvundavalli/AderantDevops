$directoriesToRemove = @(
    # Some unit tests aren't very unit-ty
    "$env:APPDATA\Aderant",
    "$env:LOCALAPPDATA\Aderant",

    # NuGet junk drawer
    "$env:APPDATA\NuGet",
    "$env:LOCALAPPDATA\NuGet",
    "$env:USERPROFILE\.nuget",

    # Shadow copy cache
    "$env:LOCALAPPDATA\assembly",

    "$env:USERPROFILE\Download",

    # VSIX extensions installed by the VS SDK targets
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\12.0Exp\Extensions\Aderant",
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\14.0Exp\Extensions\Aderant",
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\15.0Exp\Extensions\Aderant",

    $env:TEMP
)

$machineWideDirectories = @(
    "C:\Temp",
    "C:\Windows\Temp",

    # Yay for people who check in PostBuild events :)
    "C:\tfs"
)


$whoAmI = $env:USERNAME
$serviceAccounts = @("$env:USERNAME", "service.tfsbuild.ap")

foreach ($dir in $directoriesToRemove) {
    $removeTarget = $dir

    foreach ($name in $serviceAccounts) {
        $removeTarget = $removeTarget.Replace($whoAmI, $name)

        if (Test-Path $removeTarget) {
            Remove-Item $removeTarget -Verbose -Force -Recurse -ErrorAction SilentlyContinue
        } else {
            Write-Debug "Not deleting $removeTarget"
        }
    }
}


foreach ($dir in $machineWideDirectories) {
  if (Test-Path $dir) {
    Push-Location $dir
    Remove-Item * -Verbose -Force -Recurse -ErrorAction SilentlyContinue
    Pop-Location
  }
}

Get-PSDrive -PSProvider FileSystem | Select-Object -Property Root | % {$directoriesToRemove += $_.Root + "ExpertShare"}