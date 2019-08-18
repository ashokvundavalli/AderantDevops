[CmdletBinding()]
param(
    [string]$BuildScriptsDirectory = {
        # Find our caller, $MyInvocation will not work here
        $command = (Get-PSCallStack)[0].Command
        if ($command -eq "Aderant.psm1") {
            return [System.IO.Path]::GetFullPath("$PSScriptRoot\..\..\Build\")
        }

        return [System.IO.Path]::GetFullPath("$PSScriptRoot\..\")
    }.Invoke()
)

if ($PSVersionTable.PSVersion.Major -lt 5 -or ($PSVersionTable.PSVersion.Major -eq 5 -and $PSVersionTable.PSVersion.Minor -lt 1)) {
    Write-Error "PowerShell version is lower than the minimum required version of 5.1. Please update PowerShell."
    exit 1
}

function BuildProjects($BuildScriptsDirectory, [bool]$forceCompile) {
    $file = [System.IO.Path]::Combine($BuildScriptsDirectory, "..\Build.Tools\Aderant.Build.dll")
    $file = [System.IO.Path]::GetFullPath($file)

    $msbuildPath = . "$PSScriptRoot\Resolve-MSBuild" "*" "64"

    [void][System.Reflection.Assembly]::LoadFrom("$msbuildPath\Microsoft.Build.dll")
    [void][System.Reflection.Assembly]::LoadFrom("$msbuildPath\Microsoft.Build.Engine.dll")
    [void][System.Reflection.Assembly]::LoadFrom("$msbuildPath\Microsoft.Build.Tasks.Core.dll")
    [void][System.Reflection.Assembly]::LoadFrom("$msbuildPath\Microsoft.Build.Utilities.Core.dll")

    $info = [System.IO.FileInfo]::new($file)

    if ($info.Exists) {
        if ($info.Length -gt 0 -and $forceCompile -eq $false) {
            return
        }
    }

    try {
        [System.IO.File]::OpenWrite($file).Close()
    } catch {
        Write-Warning "Skipped compiling $file due to file lock."
        return
    }

    Write-Information "Preparing build environment..."

    $target = "PrepareBuildEnvironment"
    $projectPath = [System.IO.Path]::Combine($BuildScriptsDirectory, "Aderant.Build.Common.targets")

    if ($msbuildPath.Contains("2017")) {
        # 2017 is a mess of binding redirects so give up and invoke the compiler as a tool - this is slower than invoking it in-proc
        & "$msbuildPath\MSBuild.exe" $projectPath "/t:$target" "/p:project-set=minimal" "/p:BuildScriptsDirectory=$BuildScriptsDirectory"
        return
    }

    $logger = [Microsoft.Build.BuildEngine.ConsoleLogger]::new()
    if ($DebugPreference -eq 'Continue' -or $VerbosePreference -eq 'Continue') {
        $logger.Verbosity = [Microsoft.Build.Framework.LoggerVerbosity]::Normal
    } else {
        $logger.Verbosity = [Microsoft.Build.Framework.LoggerVerbosity]::Quiet
    }

    $arraylog = New-Object collections.generic.list[Microsoft.Build.Framework.ILogger]
    $arraylog.Add($logger)

    $globals = [System.Collections.Generic.Dictionary`2[System.String,System.String]]::new()
    $globals.Add("BuildScriptsDirectory", $BuildScriptsDirectory)
    $globals.Add("nologo", $null)
    $globals.Add("nr", "false")
    $globals.Add("m", $null)
    $globals.Add("project-set", "minimal")

    $params = [Microsoft.Build.Execution.BuildParameters]::new()
    $params.Loggers = $arraylog
    $params.GlobalProperties = $globals
    $params.ShutdownInProcNodeOnBuildFinish = $true

    $targets = @($target)

    $request = new-object Microsoft.Build.Execution.BuildRequestData($projectPath, $targets)

    $manager = [Microsoft.Build.Execution.BuildManager]::DefaultBuildManager
    $result = $manager.Build($params, $request)

    HandleResult $result
}

function HandleResult($result) {
    if ($result.OverallResult -ne 0) {
        if ($result.Exception) {
            throw $result.Exception
        }
    }
}

function LoadAssembly([string]$assemblyPath, [bool]$loadAsModule) {
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
                Import-Module -Assembly $assembly -DisableNameChecking -ErrorAction Stop
            }

            # Create a new dynamic module that simply loads another module into it.
            $module = New-Module -Name "Aderant.ContinuousDelivery.PowerShell" -ScriptBlock $scriptBlock -ArgumentList $assembly
            Import-Module $module -Global -DisableNameChecking -ErrorAction Stop
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

            Write-Debug "Creating mutex: $mutexName"

            $mutex = [System.Threading.Mutex]::new($false, "Local\$mutexName")

            try {
                $runTool = $mutex.WaitOne(0)
                Write-Debug "Aquired mutex: $mutexName"
            } catch [System.Threading.AbandonedMutexException] {
                $mutex.WaitOne(0)
            }

            # Prevent GC
            $MyInvocation.MyCommand.Module.PrivateData[$mutexName] = $mutex
        }
    }

    if ($runTool) {
        Write-Debug "Running action in exclusive lock"
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
    }

    Write-Information "Updating submodules..."
    $job = Start-Job -Name "submodule update" -ScriptBlock {
        Param($path)

        $result = (& git -C $path submodule update --init --recursive)
        if ($LASTEXITCODE -ne 0) {
            throw $result
        }
        return $result
    } -ArgumentList $PSScriptRoot

    $null = Register-ObjectEvent $job -EventName StateChanged -Action {
        if ($EventArgs.JobStateInfo.State -ne [System.Management.Automation.JobState]::Completed) {
            Write-Host ("Task has failed: " + $sender.ChildJobs[0].JobStateInfo.Reason.Message) -ForegroundColor red
        } else {
            $Host.UI.RawUI.WindowTitle = "Submodule update complete"
        }

        $Sender | Remove-Job -Force

        $EventSubscriber | Unregister-Event -Force
        $EventSubscriber.Action | Remove-Job -Force
    }

    $lockName = "$head" + "_BUILD_UPDATE_LOCK"

    $updated = RunActionExclusive $action $lockName

    if (-not ($updated)) {
        Write-Warning "Update skipped as another PowerShell instance is running"
    }
    return [string](& git -C $PSScriptRoot rev-parse HEAD)
}

function LoadLibGit2Sharp([string]$buildToolsDirectory) {
    [void][System.Reflection.Assembly]::LoadFrom("$buildToolsDirectory\LibGit2Sharp.dll")
}

function DownloadPaket() {
    $bootstrapper = "$BuildScriptsDirectory\paket.bootstrapper.exe"
    if (Test-Path $bootstrapper) {
        $action = {
            # Download the paket dependency tool
            Start-Process -FilePath $bootstrapper -ArgumentList  "5.198.0" -NoNewWindow -PassThru -Wait
            Start-Process -FilePath "$BuildScriptsDirectory\paket.exe" -ArgumentList @("restore", "--group", "DevTools") -NoNewWindow -PassThru -Wait -WorkingDirectory ("$BuildScriptsDirectory\..\.\..")
        }

        RunActionExclusive $action ("PAKET_UPDATE_LOCK_" + $BuildScriptsDirectory)
    } else {
        throw "FATAL: $bootstrapper does not exist."
    }
}

Set-StrictMode -Version Latest
# Without this git will look on H:\ for .gitconfig
$Env:HOME = $Env:USERPROFILE

# Redirect ERROR to OUTPUT
$Env:GIT_REDIRECT_STDERR = '2>&1'

$InformationPreference = 'Continue'
$ErrorActionPreference = 'Stop'

$isUsingProfile = $false
$command = (Get-PSCallStack)[1].Command
if ($command -eq "Aderant.psm1") {
    $isUsingProfile = $true
}

try {
    [void][Aderant.Build.BuildOperationContext]
    return
} catch {
    # Type not found - we need to bootstrap
}

$pull = $false
$branch = [Guid]::NewGuid().ToString()

if ($isUsingProfile) {
    if (-not ($Host.Name.Contains("ISE"))) {
        # ISE logs stderror as fatal. Git logs stuff to stderror and thus if any git output occurs the import will fail inside the ISE

        [string]$branch = & git -C $PSScriptRoot rev-parse --abbrev-ref HEAD

        Write-Host "`r`nBuild.Infrastructure branch [" -NoNewline

        if ($branch -eq "master" -or $branch -eq "187604-Sequencer") {
            Write-Host $branch -ForegroundColor Green -NoNewline
            Write-Host "]`r`n"
            $pull = $true
        } else {
            Write-Host $branch -ForegroundColor Yellow -NoNewline
            Write-Host "]`r`n"
        }
    }
}

$commit = RefreshSources $pull $branch

$assemblyPathRoot = [System.IO.Path]::Combine("$BuildScriptsDirectory\..\Build.Tools")

DownloadPaket

BuildProjects $BuildScriptsDirectory $isUsingProfile $commit

LoadAssembly "$assemblyPathRoot\Aderant.Build.dll" $true
LoadAssembly "$assemblyPathRoot\protobuf-net.dll" $false
LoadLibGit2Sharp "$BuildScriptsDirectory\..\Build.Tools"

[System.AppDomain]::CurrentDomain.SetData("BuildScriptsDirectory", $BuildScriptsDirectory)