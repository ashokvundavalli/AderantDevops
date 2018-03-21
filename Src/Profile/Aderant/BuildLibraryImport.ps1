[CmdletBinding()]
class ShellContext {
    ShellContext() {
        $path = "HKCU:\Software\Aderant\PowerShell"
        New-Item -Path $path -ErrorAction SilentlyContinue | Out-Null 
        $this.RegistryHome = $path 

        # Create the path to the cache if it does not exist
        New-Item -Path $this.CacheDirectory -ItemType Directory -Force -ErrorAction SilentlyContinue | Out-Null
    }
        
    [String] $BuildScriptsDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, "..\..\Build"))
    [String] $BuildToolsDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, "..\..\Build.Tools"))
    [String] $PackagingTool = [System.IO.Path]::Combine($this.BuildScriptsDirectory, "paket.exe")
    [String] $CacheDirectory = [System.IO.Path]::Combine([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData), "AderantPowerShell")
    [String] $CurrentCommit
    [String] $RegistryHome
    [bool] $IsGitRepository 
    [bool] $PoshGitAvailable 


    [object] SetRegistryValue([string]$path, [string]$name, $value) {
        $fullPath = ($this.RegistryHome + "\" + $path).TrimEnd("\")
        if (-not (Test-Path $fullPath)) {
            Write-Debug "Creating path: $fullPath"
            New-Item -Path $this.RegistryHome -Name $path -Force
        }  

        Write-Debug "Updating key: $fullPath\$name"
        $key = New-ItemProperty -Path $fullPath -Name $name -Value $value -Force

        return $key
    }

    [object] GetRegistryValue([string]$path, [string]$name) {
        if ([string]::IsNullOrWhitespace($path)) {
          $path = [string]::Empty
        }
        
        $fullPath = ($this.RegistryHome + "\" + $path).TrimEnd("\")
        
        Write-Debug "Retrieving value: $fullPath\$name"
        
        if (Test-Path -Path $fullPath) {
          try {
            $value = Get-ItemPropertyValue -Path $fullPath -Name $name -ErrorAction SilentlyContinue
            Write-Debug $value
            return $value
          } catch {
            # Property/value may not exist
          }
        }

        return $null
    }
}

$ShellContext = [ShellContext]::new()

if (Test-Path "$PSScriptRoot\DEBUG.ps1") {
  . "$PSScriptRoot\DEBUG.ps1" 
}

# Without this git will look on H:\ for .gitconfig
$Env:HOME = $Env:USERPROFILE

$GulpDirectory = (resolve-path ([System.IO.Path]::Combine($PSScriptRoot, "..\..\Gulp"))).path

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

function UpdateSubmodules([string]$head){
   # Inspect update time tracking data    
   $commit = $ShellContext.GetRegistryValue("", "LastSubmoduleCommit")   
   
    if ($commit -ne $head) {
       Write-Debug "Submodule update required"
       & git -C $PSScriptRoot submodule update --init --recursive
                
       $ShellContext.SetRegistryValue("", "LastSubmoduleCommit", $head) | Out-Null
   } else {
       Write-Debug "Submodule update not required"
   }   
}

function UpdateOrBuildAssembly($properties) {    
    $aderantBuildAssembly = [System.IO.Path]::Combine($properties.BuildToolsDirectory, "Aderant.Build.dll") 
    
    if (-not [System.IO.File]::Exists($aderantBuildAssembly)) {
        Write-Host "No Aderant.Build.dll found at $aderantBuildAssembly. Creating..."
        BuildProject $properties $true
    }    

    $outdatedAderantBuildFile = $false

    if (-not $Host.Name.Contains("ISE")) {    
        # ISE logs stderror as fatal. Git logs stuff to stderror and thus if any git output occurs the import will fail inside the ISE     

        [string]$branch = & git -C $PSScriptRoot rev-parse --abbrev-ref HEAD
        if ($branch -eq "master") {
            & git -C $PSScriptRoot pull --ff-only
        }
        [string]$head = & git -C $PSScriptRoot rev-parse HEAD
        
        UpdateSubmodules $head
    }

    Write-Host "Version: $head"

    $version = $Env:EXPERT_BUILD_VERSION

    if ($version -ne $head) {
        $outdatedAderantBuildFile = $true
    }

    if ($outdatedAderantBuildFile) {
        BuildProject $properties $true
        SetUserEnvironmentVariableNoWait "EXPERT_BUILD_VERSION" $head
    }

    $ShellContext.CurrentCommit = $head

    # Now actually load Aderant.Build.dll
    LoadAssembly $properties $aderantBuildAssembly
}

$Env:EXPERT_BUILD_DIRECTORY = Resolve-Path ([System.IO.Path]::Combine($ShellContext.BuildScriptsDirectory, "..\"))

UpdateOrBuildAssembly $ShellContext