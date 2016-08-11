$DebugPreference = 'SilentlyContinue'

# Without this git will look on H:\ for .gitconfig
$Env:HOME = $Env:USERPROFILE

function SetUserEnvironmentVariableNoWait($name, $value) {
    # SetEnvironmentVariable is very slow as it waits for apps to respond so we have this lovely async work around
Add-Type -MemberDefinition @"
public static void SetUserEnvironmentVariableNoWait(string name, string value) {
    System.Threading.ThreadPool.QueueUserWorkItem((_) => System.Environment.SetEnvironmentVariable(name, value, System.EnvironmentVariableTarget.User));
}
"@ -Name Internal -NameSpace System -UsingNamespace System.Threading

    [Internal]::SetUserEnvironmentVariableNoWait($name, $value)
}

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
    $aderantBuildAssembly = [System.IO.Path]::Combine($properties.BuildToolsDirectory, "Aderant.Build.dll")	
    
    if (-not [System.IO.File]::Exists($aderantBuildAssembly)) {
        Write-Host "No Aderant.Build.dll found at $aderantBuildAssembly. Creating..."
        BuildProject $properties $true
    }    

    $outdatedAderantBuildFile = $false

    pushd $PSScriptRoot
    [string]$branch = & git rev-parse --abbrev-ref HEAD
    if ($branch -eq "master") {
        & git pull
    }
    [string]$head = & git rev-parse HEAD
    popd   

    Write-Host "Version: $head"

    $version = $Env:EXPERT_BUILD_VERSION

    if ($version -ne $head) {        
        $outdatedAderantBuildFile = $true
    }

    if ($outdatedAderantBuildFile) {
        BuildProject $properties $true
        SetUserEnvironmentVariableNoWait "EXPERT_BUILD_VERSION" $head        
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