$DebugPreference = 'Continue'

function BuildProject($properties, [bool]$rebuild) {
    # Load the build libraries as this has our shared compile function. This function is shared by the desktop and server bootstrap of Build.Infrastructure
    $buildScripts = $properties.BuildScriptsDirectory

    if (-not (Test-Path $buildScripts)) {
        throw "Cannot find directory: $buildScripts"
        return
    }

    Write-Debug "Build scripts: $buildScripts"

    pushd $buildScripts
    Invoke-Expression ". .\Build-Libraries.ps1"
    popd	       

    CompileBuildLibraryAssembly $buildScripts $rebuild
}

function LoadAssembly($properties, [string]$targetAssembly) {
    if ([System.IO.File]::Exists($targetAssembly)) {
        Write-Host "Aderant.Build.dll found at $targetAssembly. Loading..."

        #Imports the specified modules without locking it on disk
        $assemblyBytes = [System.IO.File]::ReadAllBytes($targetAssembly)
        $pdb = [System.IO.Path]::ChangeExtension($targetAssembly, "pdb");        

        if (Test-Path $pdb) {
            Write-Debug "Importing assembly with symbols"
            $assembly = [System.Reflection.Assembly]::Load($assemblyBytes, [System.IO.File]::ReadAllBytes($pdb))
        } else {
            $assembly = [System.Reflection.Assembly]::Load($assemblyBytes)
        }

        $directory = Split-Path -Parent $targetAssembly

        [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($properties.PackagingTool)) | Out-Null
        
        Import-Module $assembly -DisableNameChecking -Global
    }
}

function UpdateOrBuildAssembly($properties) {
    Write-Debug "Profile home: $actualPath"
    $aderantBuildAssembly = [System.IO.Path]::Combine($properties.BuildToolsDirectory, "Aderant.Build.dll")	

	$needToBuild = $false
	
    if (-not [System.IO.File]::Exists($aderantBuildAssembly)) {
        Write-Host "No Aderant.Build.dll found at $aderantBuildAssembly. Creating..."
		$needToBuild = $true
    }

	if ($needToBuild -eq $true) {
		Write-Host "Building Build.Infrastructure..."
		BuildProject $properties $true
	}

    # Test if one of the files is older than a day
    $aderantBuildFileInfo = Get-ChildItem $aderantBuildAssembly	

	$outdatedAderantBuildFile = $false	
	
	$dt = $aderantBuildFileInfo.LastWriteTime.ToString("d", [System.Globalization.CultureInfo]::CurrentCulture)
	if ($aderantBuildFileInfo.LastWriteTime.Date -le [System.DateTime]::Now.AddDays(-1)) {
        Write-Host ("Aderant.Build.dll is out of date ({0}). Updating..." -f $dt)
		$outdatedAderantBuildFile = $true
    } else {
        Write-Host ("Aderant.Build.dll is not out of date. {0} is less than 1 day old" -f $dt)
    }

	if ($outdatedAderantBuildFile) {
		BuildProject $properties $true
	}

    # Now actually load Aderant.Build.dll
    LoadAssembly $properties $aderantBuildAssembly
}

$ShellContext = New-Object -TypeName PSObject
$ShellContext | Add-Member -MemberType ScriptProperty -Name BuildScriptsDirectory -Value { [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, "..\..\Build")) }
$ShellContext | Add-Member -MemberType ScriptProperty -Name BuildToolsDirectory -Value { [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, "..\..\Build.Tools")) }
$ShellContext | Add-Member -MemberType ScriptProperty -Name PackagingTool -Value { [System.IO.Path]::Combine($This.BuildScriptsDirectory, "paket.exe") }
$ShellContext | Add-Member -MemberType NoteProperty -Name IsGitRepository -Value $false
$ShellContext | Add-Member -MemberType NoteProperty -Name PoshGitAvailable -Value $false

$Env:EXPERT_BUILD_DIRECTORY = Resolve-Path ([System.IO.Path]::Combine($ShellContext.BuildScriptsDirectory, "..\"))

Write-Debug $ShellContext

UpdateOrBuildAssembly $ShellContext