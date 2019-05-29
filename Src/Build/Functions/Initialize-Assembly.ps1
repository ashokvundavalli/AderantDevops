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
        Write-Warning "Skipped compiling $aderantBuildAssembly due to file lock."
        return
    }

	[void][System.Reflection.Assembly]::Load("Microsoft.Build, Version=14.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
	[void][System.Reflection.Assembly]::Load("Microsoft.Build.Engine, Version=14.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
    [void][System.Reflection.Assembly]::Load("Microsoft.Build.Utilities.Core, Version=14.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

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
    $globals.Add("project-set", "minimal")

	$params = new-object Microsoft.Build.Execution.BuildParameters
	$params.Loggers=$arraylog
	$params.GlobalProperties=$globals

	$target="PrepareBuildEnvironment"
	$targets=@($target)

	$request = new-object Microsoft.Build.Execution.BuildRequestData($projectPath, $globals, $toolsVersion, $targets, $null)

	$manager = [Microsoft.Build.Execution.BuildManager]::DefaultBuildManager
    $manager.Build($params, $request)
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
            $module = New-Module -Name "Aderant.ContinuousDelivery.PowerShell" -ScriptBlock $scriptBlock -ArgumentList $assembly
            Import-Module $module -Global -DisableNameChecking
        }
    } else {
        throw "Fatal error. Assembly $loadAsModule not found"
    }
}

function RunActionExclusive([scriptblock]$action, [string]$mutexName) {
    $runTool = $true

    if ($null -ne $MyInvocation.MyCommand.Module) {
        if ($Host.Runspace.ApartmentState -eq [Threading.ApartmentState]::STA) {
            $mutexName = $mutexName.Replace("\", "|")
            $mutex = [System.Threading.Mutex]::new($false, "Local\$mutexName")
            $runTool = $mutex.WaitOne(0)

            # Prevent GC
            $MyInvocation.MyCommand.Module.PrivateData[$mutexName] = $mutex
        }
    }

    if ($runTool) {
        Write-Debug "Running action in exlcusive lock:"
        Write-Debug $action.Ast
        
        $action.Invoke()
    }
    return $runTool
}

function RefreshSources([bool]$pull, [string]$head) {
     $action = {
        if ($pull) {
            & git -C $PSScriptRoot pull --ff-only
        }
        & git -C $PSScriptRoot submodule update --init --recursive
    }

    $lockName = "$head" + "_build_update_lock"
    
    $updated = RunActionExclusive $action $lockName

    if (-not $updated) {
        Write-Warning "Update skipped as another PowerShell instance is running"
    }
}

function LoadLibGit2Sharp([string]$buildToolsDirectory) {
    [void][System.Reflection.Assembly]::LoadFrom("$buildToolsDirectory\LibGit2Sharp.dll")
}

function DownloadPaket([string]$buildScriptDirectory) {
    $action = {
        # Download the paket dependency tool
        Start-Process -FilePath  "$buildScriptDirectory\paket.bootstrapper.exe" -ArgumentList  "5.198.0" -NoNewWindow -PassThru -Wait
        Start-Process -FilePath  "$buildScriptDirectory\paket.exe" -ArgumentList @("restore", "--group", "DevTools") -NoNewWindow -PassThru -Wait -WorkingDirectory ("$BuildScriptsDirectory\..\.\..")
    }

    RunActionExclusive $action ("458c7732-8934-42e8-8e93-afcc8632c632" + $buildScriptDirectory)
}

function global:UpdateOrBuildAssembly {
  Param(
        [Parameter(Mandatory=$true)]
        [string]
        $BuildScriptsDirectory,

        [Parameter(Mandatory=$true)]
        [bool]
        $Update,

        [Parameter(Mandatory=$false)]
        [bool]
        $IsServerBuild
    )

    begin {
        Set-StrictMode -Version 'Latest'
    }

    process {
        try {
            [void][Aderant.Build.BuildOperationContext]
            return
        } catch {
            # Type not found - we need to bootstrap
        }

        if ($PSVersionTable.PSVersion.Major -lt 5 -or ($PSVersionTable.PSVersion.Major -eq 5 -and $PSVersionTable.PSVersion.Minor -lt 1)) {
            Write-Error "PowerShell version is lower than the minimum required version of 5.1 to compile Aderant.Build. Please update PowerShell."
            exit 1
        }

        # Redirect ERROR to OUTPUT
        $env:GIT_REDIRECT_STDERR = '2>&1'

        if ($Update) {
            if (-not $Host.Name.Contains("ISE")) {
                # ISE logs stderror as fatal. Git logs stuff to stderror and thus if any git output occurs the import will fail inside the ISE

                [string]$branch = & git -C $PSScriptRoot rev-parse --abbrev-ref HEAD

                Write-Host "`r`nBuild.Infrastructure branch [" -NoNewline

                $pull = $false

                if ($branch -eq "master" -or $branch -eq "187604-Sequencer") {
                    Write-Host $branch -ForegroundColor Green -NoNewline
                    Write-Host "]`r`n"
                    $pull = $true
                } else {
                    Write-Host $branch -ForegroundColor Yellow -NoNewline
                    Write-Host "]`r`n"
                }                

                if ($IsServerBuild) {
                    $pull = $false
                }
                
                RefreshSources $pull $branch
            }
        }

        $assemblyPathRoot = [System.IO.Path]::Combine("$BuildScriptsDirectory\..\Build.Tools")

        DownloadPaket $BuildScriptsDirectory

        BuildProjects $BuildScriptsDirectory $true

        LoadAssembly $BuildScriptsDirectory "$assemblyPathRoot\Aderant.Build.dll" $true
        LoadAssembly $BuildScriptsDirectory "$assemblyPathRoot\protobuf-net.dll" $false
        LoadLibGit2Sharp "$BuildScriptsDirectory\..\Build.Tools"

        [System.AppDomain]::CurrentDomain.SetData("BuildScriptsDirectory", $BuildScriptsDirectory)
    }
}