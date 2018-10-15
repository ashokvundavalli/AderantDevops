# Without this git will look on H:\ for .gitconfig
$Env:HOME = $Env:USERPROFILE

function GetBuildLibraryAssemblyPath([string]$buildScriptDirectory) {
    $file = [System.IO.Path]::Combine($buildScriptDirectory, "..\Build.Tools\Aderant.Build.dll")
    return [System.IO.Path]::GetFullPath($file)
}

function BuildProjects($buildScriptDirectory, [bool]$forceCompile) {	
    $aderantBuildAssembly = GetBuildLibraryAssemblyPath $buildScriptDirectory

    if ([System.IO.File]::Exists($aderantBuildAssembly) -and $forceCompile -eq $false) {
        return
    }

    try {
        [System.IO.File]::OpenWrite($aderantBuildAssembly).Close()
    } catch {
        Write-Debug "Skipped compiling $aderantBuildAssembly due to file lock."
        return
    }
  
	$build = [System.Reflection.Assembly]::Load("Microsoft.Build, Version=14.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
	$buildEngine = [System.Reflection.Assembly]::Load("Microsoft.Build.Engine, Version=14.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
    $buildUtilities = [System.Reflection.Assembly]::Load("Microsoft.Build.Utilities.Core, Version=14.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
    $toolsVersion = "14.0";
    Write-Debug "Loaded MS Build 14.0"   
    $projectPath = [System.IO.Path]::Combine($buildScriptDirectory, "Aderant.Build.Common.targets")
	
    $logger = new-Object Microsoft.Build.BuildEngine.ConsoleLogger
	$logger.Verbosity = [Microsoft.Build.Framework.LoggerVerbosity]::Quiet
    $arraylog = New-Object collections.generic.list[Microsoft.Build.Framework.ILogger]               
    $arraylog.Add($logger)

	$globals = New-Object 'System.Collections.Generic.Dictionary[String,String]'
	$globals.Add("BuildScriptsDirectory", $buildScriptDirectory)
	$globals.Add("nologo", $null)
	$globals.Add("nr", "false")
	$globals.Add("m", $null)

	$params = new-object Microsoft.Build.Execution.BuildParameters
	$params.Loggers=$arraylog
	$params.GlobalProperties=$globals
	
	$target="PrepareBuildEnvironment"
	$targets=@($target)	
	
	$request = new-object Microsoft.Build.Execution.BuildRequestData($projectPath, $globals, $toolsVersion, $targets, $null)
	
	[Microsoft.Build.Execution.BuildManager]::DefaultBuildManager.Build($params, $request)
}

function LoadAssembly($buildScriptsDirectory, [string]$assemblyPath, [bool]$loadAsModule) {
    Set-StrictMode -Version "Latest"
    $ErrorActionPreference = "Stop"

    if ([System.IO.File]::Exists($assemblyPath)) {
        [System.Reflection.Assembly]$assembly = $null

        #Loads the assembly without locking it on disk
        $assemblyBytes = [System.IO.File]::ReadAllBytes($assemblyPath)
        $pdb = [System.IO.Path]::ChangeExtension($assemblyPath, "pdb");

        if (Test-Path $pdb) {
            Write-Debug "Importing assembly $assemblyPath with symbols"
            $assembly = [System.Reflection.Assembly]::Load($assemblyBytes, [System.IO.File]::ReadAllBytes($pdb))
        } else {
            Write-Debug "Importing assembly $assemblyPath without symbols"
            $assembly = [System.Reflection.Assembly]::Load($assemblyBytes)
        }

        if ($loadAsModule) {
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
        }
    } else {
        throw "Fatal error. Assembly $loadAsModule not found"
    }
}

function UpdateSubmodules([string]$head) {
    #TODO speed this up    
    Set-StrictMode -Version 'Latest'  
    & git -C $PSScriptRoot submodule update --init --recursive
}

function LoadLibGit2Sharp([string]$buildToolsDirectory) {    
    [void][System.Reflection.Assembly]::LoadFrom("$buildToolsDirectory\LibGit2Sharp.dll")
}

function global:UpdateOrBuildAssembly {
  Param(
        [Parameter(Mandatory=$true)]        
        [string]
        $BuildScriptsDirectory,

        [Parameter(Mandatory=$true)]        
        [bool]
        $Update
    )

    Set-StrictMode -Version 'Latest'

    try {
        [void][Aderant.Build.BuildOperationContext]
        return
    } catch {
        # Type not found - we need to bootstrap
    }

    if ($Update) {
        if ($Host.Name.Contains("ISE")) {    
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
    }

    $assemblyPathRoot = [System.IO.Path]::Combine("$BuildScriptsDirectory\..\Build.Tools")

    BuildProjects $BuildScriptsDirectory $true

    LoadAssembly $BuildScriptsDirectory "$assemblyPathRoot\Aderant.Build.dll" $true
    LoadAssembly $BuildScriptsDirectory "$assemblyPathRoot\paket.exe" $false
    LoadAssembly $BuildScriptsDirectory "$assemblyPathRoot\protobuf-net.dll" $false    

    LoadLibGit2Sharp "$BuildScriptsDirectory\..\Build.Tools"
}