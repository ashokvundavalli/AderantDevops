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

function GetAlternativeStreamValue {
    [CmdletBinding()]
    param (
        $File,
        [string] $StreamName
    )
    return Get-Content -Path $File -Stream $StreamName -ErrorAction "SilentlyContinue"
}

function SetAlternativeStreamValue {
    [CmdletBinding()]
    param (
        $File,
        [string] $StreamName,
        $Value
    )

    Set-Content -Path $File -Value $Value -Stream $StreamName
}

Set-StrictMode -Version "Latest"
$buildCommitStreamName = "Build.Commit"

function DoActionIfNeeded([scriptblock]$action, $file) {
    $version = GetAlternativeStreamValue $file $buildCommitStreamName

    $commit = $ExecutionContext.SessionState.Module.PrivateData[$buildCommitStreamName]

    if ($version -ne $commit) {
        Write-Debug "Running action $($action.Ast) for $file because '$version' != '$commit'"

        $action.Invoke()
        SetAlternativeStreamValue $file $buildCommitStreamName $commit
    } else {
        Write-Debug "Skipping $($action.Ast) for $file because '$version' == '$commit'"
    }
}

function BuildProjects([string]$mainAssembly, [bool]$forceCompile, [string]$commit) {
    $msbuildPath = . "$PSScriptRoot\Resolve-MSBuild" "*" "x86"

    [void][System.Reflection.Assembly]::LoadFrom("$msbuildPath\Microsoft.Build.dll")
    [void][System.Reflection.Assembly]::LoadFrom("$msbuildPath\Microsoft.Build.Engine.dll")
    [void][System.Reflection.Assembly]::LoadFrom("$msbuildPath\Microsoft.Build.Tasks.Core.dll")
    [void][System.Reflection.Assembly]::LoadFrom("$msbuildPath\Microsoft.Build.Utilities.Core.dll")

    $info = [System.IO.FileInfo]::new($mainAssembly)

    if ($info.Exists) {
        if ($info.Length -gt 0 -and $forceCompile -eq $false) {
            return
        }

        $buildCommit = GetAlternativeStreamValue $info.FullName $buildCommitStreamName

        if ($buildCommit -eq $commit) {
            Write-Debug "Skipped compiling $info as it was for your current commit."
            return
        }
    }

    try {
        $info.OpenWrite().Close()
    } catch {
        Write-Warning "Skipped compiling $info due to file lock."
        return
    }

    Write-Information "Preparing build environment..."

    $target = "PrepareBuildEnvironment"
    $projectPath = [System.IO.Path]::Combine($BuildScriptsDirectory, "Aderant.Build.Common.targets")

    $bootstrap = {
        try {
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
        } finally {
            SetAlternativeStreamValue $info.FullName $buildCommitStreamName $commit
        }
    }

    $timeTaken = (Measure-Command $bootstrap)
    Write-Information "Projects compiled: $timeTaken"
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
        $assembly = [System.Reflection.Assembly]::Load($assemblyBytes)

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

function RunActionExclusive {
    [OutputType([bool])]
    param(
        [ScriptBlock]$Action,
        [string]$MutexName
    )

    $runTool = $true

    if ($null -ne $MyInvocation.MyCommand.Module) {
        if ($Host.Runspace.ApartmentState -eq [Threading.ApartmentState]::STA) {
            $mutexName = $MutexName.Replace("\", "_").Replace(":", "")

            Write-Debug "Creating mutex: $MutexName"
            $mutex = [System.Threading.Mutex]::new($false, "Local\$MutexName")

            try {
                $runTool = $mutex.WaitOne(0)
                Write-Debug "Aquired mutex $MutexName->$runTool"
            } catch [System.Threading.AbandonedMutexException] {
                # Since we cannot clean up the mutex reliably we have to cater for the mutex being abandoned.
                # This can occur everytime PowerShell is closed as the console host does not give us a chance to clean up our process.
                # We could handle console_ctrl_handler but that seems like a lot of work.
                $runTool = $mutex.WaitOne(0)
            }

            # Prevent GC
            $MyInvocation.MyCommand.Module.PrivateData[$mutexName] = $mutex
        }
    }

    if ($runTool) {
        if ($DebugPreference -ne "SilentlyContinue") {
            Write-Debug "Running action in exclusive lock $MutexName"
            Write-Debug $Action.Ast
        }

        [void]$Action.Invoke()
    }

    return $runTool
}

function UpdateSubmodules {
    param(
       [bool]$Updated
    )

    if (-not ($Updated)) {
        Write-Information "Submodule update skipped as another PowerShell instance is running"
    } else {
        # Only update submodules if we tried to update since we may have a new commit
        # This is quite slow
        Write-Information "Updating submodules..."

        $job = Start-JobInProcess -Name "submodule update" -ScriptBlock {
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
                $millisecondsTaken = [int]($Sender.PSEndTime - $Sender.PSBeginTime).TotalMilliseconds
                $Host.UI.RawUI.WindowTitle = "Submodule update complete ($millisecondsTaken ms)"
            }

            $Sender | Remove-Job -Force

            $EventSubscriber | Unregister-Event -Force
            $EventSubscriber.Action | Remove-Job -Force
        }
    }
}

function RefreshSource {
    param(
       [bool]$Pull,
       [string]$Branch
    )

    $action = {
       if ($Pull) {
           & git -C $PSScriptRoot pull --ff-only
       }
    }

    $lockName = $Branch + "_BUILD_UPDATE_LOCK"
    $updated = RunActionExclusive $action $lockName

    $version = [string](& git -C $PSScriptRoot rev-parse HEAD)

    return [PsCustomObject]@{ Updated = $updated; Version = $version }
}

function LoadLibGit2Sharp([string]$buildToolsDirectory) {
    [void][System.Reflection.Assembly]::LoadFrom("$buildToolsDirectory\LibGit2Sharp.dll")
}

function SetNuGetProviderPath([string]$buildToolsDirectory) {
    # Submodules are initialized asynchronously so this path may not exist when this function is called
    $credentialProviderPath = [System.IO.Path]::Combine($buildToolsDirectory, "NuGet.CredentialProvider")
    [System.Environment]::SetEnvironmentVariable("NUGET_CREDENTIALPROVIDERS_PATH", $credentialProviderPath, [System.EnvironmentVariableTarget]::Process)
}

function DownloadPaket([string]$commit) {
    $bootstrapper = "$BuildScriptsDirectory\paket.bootstrapper.exe"

    if (Test-Path $bootstrapper) {
        $paketExecutable = "$BuildScriptsDirectory\paket.exe"

        $value = GetAlternativeStreamValue $paketExecutable $buildCommitStreamName

        if ($value -eq $commit) {
            Write-Debug "Skipping paket update because '$value' == '$commit'"
            return
        }

        $action = {
            # Download the paket dependency tool
            Start-Process -FilePath $bootstrapper -ArgumentList  "5.198.0" -NoNewWindow -PassThru -Wait
            Start-Process -FilePath $paketExecutable -ArgumentList @("restore", "--group", "DevTools") -NoNewWindow -PassThru -Wait -WorkingDirectory ("$BuildScriptsDirectory\..\.\..")
        }

        RunActionExclusive $action ("PAKET_UPDATE_LOCK_" + $BuildScriptsDirectory)
        SetAlternativeStreamValue $paketExecutable $buildCommitStreamName $commit
    } else {
        throw "FATAL: $bootstrapper does not exist."
    }
}

function EnsureClientCertificateAvailable() {
    try {
        $store = [System.Security.Cryptography.X509Certificates.X509Store]::new("My", "CurrentUser")
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
        $certificates = $store.Certificates.Find([System.Security.Cryptography.X509Certificates.X509FindType]::FindByApplicationPolicy, "1.3.6.1.5.5.7.3.2", $true)

        if ($certificates.Count -eq 0) {
            # Request a client certificate if we are a service account
            $username = [Environment]::UserName
            if ($username.EndsWith("$")) {
                Get-Certificate -Template "ADERANTgMSAUser" -Url "ldap:" -SubjectName "CN=$username" -CertStoreLocation "Cert:\CurrentUser\My"
            } else {
                Write-Warning "No certificates for client authentication are available"
            }
        }
    } finally {
        $store.Dispose()
    }
}

function EnsureServiceEndpointsAvailable() {
    if ([System.Environment]::GetEnvironmentVariable("SYSTEM_TEAMFOUNDATIONCOLLECTIONURI")) {
        $serviceConnectionName = Get-VstsInput -Name "nuGetServiceConnection" -Require
        $endpoint = Get-VstsEndpoint -Name $serviceConnectionName -Require

        # Connection API key can be accessed via $endpoint.Auth.parameters.nugetkey
    }
}

function LoadVstsTaskLibrary {
    $vstsTaskLib = [System.Environment]::GetEnvironmentVariable("VSTS_TASK_LIB_HOME")
    if ($vstsTaskLib) {
        $taskModule = [System.IO.Path]::Combine($vstsTaskLib, "VstsTaskSdk.psd1")
        Import-Module -Name $taskModule -ArgumentList @{ NonInteractive = $true }
    } else {
        $vstsTaskLib = (Get-Module VstsTaskSdk)
        if ($vstsTaskLib) {        
            $taskHomeDirectory = $vstsTaskLib.ModuleBase
            [System.Environment]::SetEnvironmentVariable("VSTS_TASK_LIB_HOME", $taskHomeDirectory, [System.EnvironmentVariableTarget]::Process)
            Write-Debug "Set VSTS_TASK_LIB_HOME => $taskHomeDirectory"
        }
    }
}

try {
    [void][Aderant.Build.BuildOperationContext]
    return
} catch {
    # Type not found - we need to bootstrap
}

$originalErrorActionPreference = $ErrorActionPreference
try {
    # Without this git will look on H:\ for .gitconfig
    $Env:HOME = $Env:USERPROFILE

    # Redirect ERROR to OUTPUT
    [System.Environment]::SetEnvironmentVariable("GIT_REDIRECT_STDERR", "2>&1", [System.EnvironmentVariableTarget]::Process)

    $isUsingProfile = ($null -ne $ExecutionContext.SessionState.Module)

    $InformationPreference = 'Continue'
    $ErrorActionPreference = 'Stop'

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

    $updateInfo = RefreshSource $pull $branch
    $commit = $updateInfo.Version

    . "$PSScriptRoot\InProcessJobs.ps1" -Version $commit

    UpdateSubmodules ($updateInfo.Updated)

    $assemblyPathRoot = [System.IO.Path]::Combine("$BuildScriptsDirectory\..\Build.Tools")
    $mainAssembly = "$assemblyPathRoot\Aderant.Build.dll"

    DownloadPaket $commit
    BuildProjects $mainAssembly $isUsingProfile $commit
    LoadAssembly  $mainAssembly $true
    LoadAssembly "$assemblyPathRoot\protobuf-net.dll" $false
    LoadLibGit2Sharp $assemblyPathRoot
    LoadVstsTaskLibrary
    SetNuGetProviderPath $assemblyPathRoot
    EnsureClientCertificateAvailable
    EnsureServiceEndpointsAvailable

    [System.AppDomain]::CurrentDomain.SetData("BuildScriptsDirectory", $BuildScriptsDirectory)

    if ($isUsingProfile) {
        $ExecutionContext.SessionState.Module.PrivateData[$buildCommitStreamName] = $commit
    }
} finally {
    $ErrorActionPreference = $originalErrorActionPreference
}