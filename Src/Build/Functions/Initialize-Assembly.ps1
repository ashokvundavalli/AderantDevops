# Without this git will look on H:\ for .gitconfig
$Env:HOME = $Env:USERPROFILE

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
    Set-StrictMode -Version 'Latest'

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
        
        Get-Module -Name "Aderant" | Remove-Module -Force        
        Get-Module -Name "dynamic_code_module_Aderant.Build, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" | Remove-Module -Force

        Import-Module $assembly -DisableNameChecking -Global -Force
    } else {
        throw "Fatal error. Profile assembly not found"
    }
}

function UpdateSubmodules([string]$head) {
    Set-StrictMode -Version 'Latest'
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
    Set-StrictMode -Version 'Latest'

    $aderantBuildAssembly = [System.IO.Path]::Combine($properties.BuildToolsDirectory, "Aderant.Build.dll") 
    
    if (-not [System.IO.File]::Exists($aderantBuildAssembly)) {
        Write-Host "No Aderant.Build.dll found at $aderantBuildAssembly. Creating..."
        BuildProject $properties $true
    }    

    $outdatedAderantBuildFile = $false

    if (-not $Host.Name.Contains("ISE")) {    
        # ISE logs stderror as fatal. Git logs stuff to stderror and thus if any git output occurs the import will fail inside the ISE     

        [string]$branch = & git -C $PSScriptRoot rev-parse --abbrev-ref HEAD

        Write-Host "`r`nBuild.Infrastructure branch [" -NoNewline

        if ($branch -eq "master") {
            Write-Host $branch -ForegroundColor Green -NoNewline
            Write-Host "]`r`n"
            & git -C $PSScriptRoot pull --ff-only
        } else {
            Write-Host $branch -ForegroundColor Yellow -NoNewline
            Write-Host "]`r`n"
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
    }

    $ShellContext.CurrentCommit = $head

    # Now actually load Aderant.Build.dll
    LoadAssembly $properties $aderantBuildAssembly
}

$Env:EXPERT_BUILD_DIRECTORY = Resolve-Path ([System.IO.Path]::Combine($ShellContext.BuildScriptsDirectory, "..\"))

UpdateOrBuildAssembly $ShellContext