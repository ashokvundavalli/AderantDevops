<#
.Synopsis
    Gets the UI automation test framework and it's dependencies.
.Parameter $UiFrameworkDirectory
    The drop output of aderant's UI Test Automation Framework.
.Parameter $TargetDirectory
    The target location for these dependencies.
.Parameter $BuildScriptsDirectory
	The build scripts directory.
#>
param([string]$UiFrameworkDirectory, [string]$TargetDirectory, [string]$BuildScriptsDirectory)

begin {    
    #[string]$moduleDropPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($dropRootUNCPath, $assemblyFileVersion))   
    
    $shell = Join-Path $BuildScriptsDirectory Build-Libraries.ps1
    &($shell)
}

process {
	$source = PathToLatestSuccessfulBuild $UiFrameworkDirectory
	Write-Host "Copying $source to $TargetDirectory"
	robocopy.exe /NJS /NJH /NS /NC /NFL /NDL /S /R:2 /W:5  $source $TargetDirectory
}

end {
    write "Copied Test Dependencies from $source to $TargetDirectory"
}

