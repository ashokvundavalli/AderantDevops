$directoriesToRemove = @(
    # Some unit tests aren't very unit-ty
    "$env:APPDATA\Aderant",
    "$env:LOCALAPPDATA\Aderant",
    
    # Shadow copy cache
    "$env:LOCALAPPDATA\assembly",
        
    "$env:USERPROFILE\Download",
    
    # VSIX extensions installed by the VS SDK targets
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\12.0Exp\Extensions\Aderant",
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\14.0Exp\Extensions\Aderant",

    $env:TEMP,    
    "C:\Temp",
    
    # Yay for people who check in PostBuild events :)
    "C:\tfs"
)

Get-PSDrive -PSProvider FileSystem | Select-Object -Property Root | % {$directoriesToRemove += $_.Root + "ExpertShare"}

foreach ($dir in $directoriesToRemove) {
    Remove-Item $dir -Verbose -Force -Recurse -ErrorAction SilentlyContinue  
}

