# Without this git will look on H:\ for .gitconfig
$Env:HOME = $Env:USERPROFILE
$coreAssemblyName = "Aderant.Build.dll"

function BuildProject($buildScriptsDirectory, [bool]$rebuild) {
    Write-Debug "Build scripts: $buildScriptsDirectory"

    if (-not (Test-Path $buildScriptsDirectory)) {
        throw "Cannot find directory: $buildScriptsDirectory"
        return
    }    

    # Load the build libraries as this has our shared compile function.
    # This function is shared by the desktop and server bootstrap of Build.Infrastructure       
    . $buildScriptsDirectory\Build-Libraries.ps1   

    CompileBuildLibraryAssembly $buildScriptsDirectory $rebuild
}

function LoadAssembly($buildScriptsDirectory, [string]$targetAssembly) {
    Set-StrictMode -Version "Latest"
    $ErrorActionPreference = "Stop"

    if ([System.IO.File]::Exists($targetAssembly)) {
        [System.Reflection.Assembly]$assembly = $null

        #Loads the assembly without locking it on disk
        $assemblyBytes = [System.IO.File]::ReadAllBytes($targetAssembly)
        $pdb = [System.IO.Path]::ChangeExtension($targetAssembly, "pdb");

        if (Test-Path $pdb) {
            Write-Debug "Importing assembly with symbols"
            $assembly = [System.Reflection.Assembly]::Load($assemblyBytes, [System.IO.File]::ReadAllBytes($pdb))
        } else {
            $assembly = [System.Reflection.Assembly]::Load($assemblyBytes)
        }

        $directory = Split-Path -Parent $targetAssembly

        #[System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($properties.PackagingTool)) | Out-Null

        # This load process was built after many days of head scratching trying to get -Global to work.
        # The -Global switch appears to be bug ridden with compiled modules not backed by an on disk assembly which is our use case.
        # Even with the -Global flag the commands within the module are not imported into the global space.
        # Creating a runtime module to wrap the import of the compiled module works around
        # whatever PS bug prevents it from working.
        $scriptBlock = {
            param($assembly)      
            Import-Module -Assembly $assembly -DisableNameChecking
        }

        # Create a new dynamic module that simply loads another module into it.
        $module = New-Module -ScriptBlock $scriptBlock -ArgumentList $assembly
        Import-Module $module -Global
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

function LoadLibGit2Sharp([string]$buildToolsDirectory) {
    #[Environment]::SetEnvironmentVariable("LibGit2SharpLibraryPath", $buildToolsDirectory, [System.EnvironmentVariableTarget]::Process)
    [System.Reflection.Assembly]::LoadFrom("$buildToolsDirectory\LibGit2Sharp.dll")

}

function UpdateOrBuildAssembly([string]$buildScriptsDirectory) {    
    Set-StrictMode -Version 'Latest'    

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

    $assemblyPath = [System.IO.Path]::Combine("$buildScriptsDirectory\..\Build.Tools", $coreAssemblyName)     
    BuildProject $buildScriptsDirectory $true
    LoadAssembly $buildScriptsDirectory $assemblyPath
    LoadLibGit2Sharp "$buildScriptsDirectory\..\Build.Tools"
}