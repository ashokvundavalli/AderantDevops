[CmdletBinding()]
param(
    [string]$BuildScriptsDirectory = {
        return [System.IO.Path]::GetFullPath("$PSScriptRoot\..\")
    }.Invoke()
)

$script:currentCommit = $null
$script:isUsingProfile = $null

if ($PSVersionTable.PSVersion.Major -lt 5 -or ($PSVersionTable.PSVersion.Major -eq 5 -and $PSVersionTable.PSVersion.Minor -lt 1)) {
    Write-Error "PowerShell version is lower than the minimum required version of 5.1. Please update PowerShell."
    exit 1
}

$NETVersion = Get-ItemProperty "HKLM:SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"
if (-Not $NETVersion.Version.StartsWith('4.8')) { 
    Write-Warning "Please install Microsoft .NET Framework 4.8 SDK from https://dotnet.microsoft.com/download/dotnet-framework/net48." 
    Write-Warning "The installed .NET Framework Version is $($NETVersion.Version)"
}

[string]$script:repositoryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($BuildScriptsDirectory, "..\..\"))

function GetAlternativeStreamValue {
    [CmdletBinding()]
    param (
        [string]$File,
        [string]$StreamName
    )

    return Get-Content -Path $File -Stream $StreamName -ErrorAction "SilentlyContinue"
}

function SetAlternativeStreamValue {
    [CmdletBinding()]
    param (
        [string]$File,
        [string]$StreamName,
        [string]$Value
    )

    Set-Content -Path $File -Value $Value -Stream $StreamName
}

Set-StrictMode -Version "Latest"
$buildCommitStreamName = "Build.Commit"

function DoActionIfNeeded([scriptblock]$action, [string]$file) {
    $version = GetAlternativeStreamValue -File $file -StreamName $buildCommitStreamName

    $commit = $script:currentCommit

    if ($null -eq $commit -or $version -ne $commit) {
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
        # Should a build fail we may end up with a zero byte file
        if ($info.Length -gt 0) {
          if ($forceCompile -eq $false) {
              return
          }

          $buildCommit = GetAlternativeStreamValue $info.FullName $buildCommitStreamName

          if ($buildCommit -eq $commit) {
              Write-Debug "Skipped compiling $info as it was for your current commit."
              return
          }
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
                & "$msbuildPath\MSBuild.exe" $projectPath "/t:$target" "/p:project-set=minimal" "/p:BuildScriptsDirectory=$BuildScriptsDirectory" "/nr:false"
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
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Please try executing on PowerShell: MSBuild.exe $($projectPath) /t:$($target) /p:project-set=minimal /p:BuildScriptsDirectory=$($BuildScriptsDirectory) /nr:false"
                throw "FATAL: Compile failed."
            }
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

function LoadAssembly {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$assemblyPath,
        [bool]$loadAsModule
    )

    if ([System.IO.File]::Exists($assemblyPath)) {
        [System.Reflection.Assembly]$assembly = $null

        #Loads the assembly without locking it on disk
        $assemblyBytes = [System.IO.File]::ReadAllBytes($assemblyPath)

        try {
            $assembly = [System.Reflection.Assembly]::Load($assemblyBytes)
        } catch [System.BadImageFormatException] {
            Write-Error "Failed to load $assemblyPath $_"
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
        throw "Fatal error. Assembly $assemblyPath not found"
    }
}

function RunActionExclusive {
    [OutputType([bool])]
    param(
        [ScriptBlock]$Action,
        [string]$MutexName
    )

    $runTool = $true

    if ($script:isUsingProfile) {
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
            [System.AppDomain]::CurrentDomain.SetData($mutexName, $mutex)
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
       [bool]$Updated,
       [string]$Commit
    )

    $action = {
        # Only update submodules if we tried to update since we may have a new commit
        # This is quite slow
        Write-Information "Updating submodules..."

        [string]$root = git.exe -C $PSScriptRoot rev-parse --show-toplevel
        [string[]]$submodules = git.exe -C $root submodule status
        if ($LASTEXITCODE -ne 0) {
            throw $submodules
        }

        Write-Information "Submodules to be updated: $submodules"

        foreach ($submodule in $submodules) {
            [string]$submodulePath = $submodule.Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)[1]
            [string]$submoduleName = $submodulePath.Substring($submodulePath.LastIndexOf('/') + 1)

            if ($submoduleName -eq 'DevTools' -and -not $script:isUsingProfile) {
                continue
            }

            [ScriptBlock]$submoduleUpdate = {
                param(
                    [string]$root,
                    [string]$submodulePath
                )

                $result = (& git.exe -C $root submodule update --init --recursive --remote "./$submodulePath")
                if ($LASTEXITCODE -ne 0) {
                    throw $result
                }

                return $result
            }

            Write-Information -MessageData "Updating submodule: $submoduleName with path: './$submodulePath'."
            $job = Start-JobInProcess -Name "submodule_update_$submoduleName" -ScriptBlock $submoduleUpdate -ArgumentList $root, $submodulePath

            $null = Register-ObjectEvent $job -MessageData $submoduleName -EventName StateChanged -Action {
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

    $markerFile = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "Submodule_" + $commit) 
    DoActionIfNeeded $action $markerFile
}

function RefreshSource {
    param(
       [bool]$Pull,
       [string]$Branch
    )

    [System.Enum]$originalDebugPreference = $DebugPreference

    if (-not $script:isUsingProfile) {
        $DebugPreference = 'Continue' 
    }

    $initialVersion = [string](& git -C $PSScriptRoot rev-parse HEAD)
    Write-Debug "Initial Version: $($initialVersion)"

    $action = {
       if ($Pull) {
           & git -C $PSScriptRoot pull --ff-only
       }
    }

    $lockName = $Branch + "_BUILD_UPDATE_LOCK"
    Write-Debug "Lock Name: $($lockName)"

    $updated = RunActionExclusive $action $lockName

    $updatedVersion = [string](& git -C $PSScriptRoot rev-parse HEAD)
    Write-Debug "Update Version: $($updatedVersion)"

    if ($updated -and $initialVersion -eq $updatedVersion) {
        $updated = $false
    }

    $DebugPreference = $originalDebugPreference

    return [PsCustomObject]@{ Updated = $updated; Version = $updatedVersion }
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
        $paketExecutable = [System.IO.Path]::Combine($BuildScriptsDirectory, "paket.exe")
        $packageDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($script:repositoryRoot, "packages\devtools"))

        $value = GetAlternativeStreamValue $paketExecutable $buildCommitStreamName

        if (-not [System.IO.Directory]::Exists($packageDirectory)) {
            $value = [string]::Empty
        }

        if ($value -eq $commit) {
            Write-Debug "Skipping paket update because '$value' == '$commit'"
            return
        }

        [string]$paketVersion = Get-Content -Path ([System.IO.Path]::Combine($script:repositoryRoot, "Build\paket.version"))

        $action = {
            # Download the paket dependency tool
            Start-Process -FilePath $bootstrapper -ArgumentList $paketVersion -NoNewWindow -PassThru -Wait
            [void](New-Item -Path $packageDirectory -ItemType 'Directory' -Force)
            Start-Process -FilePath $paketExecutable -ArgumentList @("restore", "--group", "DevTools") -NoNewWindow -PassThru -Wait -WorkingDirectory $script:repositoryRoot
        }

        [void](RunActionExclusive $action ("PAKET_UPDATE_LOCK_" + $BuildScriptsDirectory))
        SetAlternativeStreamValue $paketExecutable $buildCommitStreamName $commit

    } else {
        throw "FATAL: $bootstrapper does not exist."
    }
}

function UpdateMsBuildTasks {
    [string]$mSBuildTasksPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($script:repositoryRoot, "packages\devtools\MSBuildTasks\tools"))

    if ([System.IO.Directory]::Exists($mSBuildTasksPath)) {
        [System.IO.FileInfo[]]$files = Get-ChildItem -Path $mSBuildTasksPath -Filter 'MSBuild.Community.Tasks.*' -File

        [string]$mSBuildTasksDestination = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\Tasks\MSBuild.Community.Tasks\")

        if (-not [System.IO.Directory]::Exists($mSBuildTasksDestination)) {
            [void](New-Item -Path $mSBuildTasksDestination -ItemType 'Directory' -Force)
        }

        foreach ($file in $files) {
            [string]$location = $file.FullName
            [string]$destination = [System.IO.Path]::Combine($mSBuildTasksDestination, $file.Name)

            [ScriptBlock]$copyAction = {
                if (-not [System.IO.File]::Exists($destination)) {
                    Copy-Item -Path $location -Destination $destination -Force
                }
            }
            $copyAction = $copyAction.GetNewClosure()
            DoActionIfNeeded -action $copyAction -File $location
        }
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
                Write-Warning "No certificates for client authentication are available."
            }
        }
    } finally {
        $store.Dispose()
    }
}

function EnsureServiceEndpointsAvailable() {
    if ([System.Environment]::GetEnvironmentVariable("SYSTEM_TEAMFOUNDATIONCOLLECTIONURI")) {
        $serviceConnectionName = Get-VstsInput -Name "nuGetServiceConnection" -Require
        Write-Host "nuGetServiceConnection: $serviceConnectionName"
        $endpoint = Get-VstsEndpoint -Name $serviceConnectionName -Require
        Write-Host "VstsEndpoint: $endpoint"

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

function SetTimeouts {
    $timeoutMillseconds = [TimeSpan]::FromMinutes(5).TotalMilliseconds

    $Env:PAKET_SKIP_RESTORE_TARGETS = "true"

    # Timeout for the request
    $Env:PAKET_REQUEST_TIMEOUT = $timeoutMillseconds

    #Timeout for the response of the request
    $Env:PAKET_RESPONSE_STREAM_TIMEOUT = $timeoutMillseconds

    # Timeout for streaming the read and write operations
    $Env:PAKET_STREAMREADWRITE_TIMEOUT = $timeoutMillseconds
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

    # Where we loaded from the default profile script?
    $isUsingProfile = ($null -ne (Get-PSCallStack | Where-Object { $_.Command.EndsWith("_profile.ps1") }))

    $InformationPreference = 'Continue'
    $ErrorActionPreference = 'Stop'

    $pull = $false
    $branch = [Guid]::NewGuid().ToString()

    if ($isUsingProfile) {
        if (-not ($Host.Name.Contains("ISE"))) {
            # ISE logs stderror as fatal. Git logs stuff to stderror and thus if any git output occurs the import will fail inside the ISE

            [string]$branch = & git -C $PSScriptRoot rev-parse --abbrev-ref HEAD

            Write-Host "`r`nBuild.Infrastructure branch [" -NoNewline

            if ($branch -eq 'master') {
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

    if ($isUsingProfile) {
        $script:currentCommit = $commit
    }

    . "$PSScriptRoot\InProcessJobs.ps1" -Version $commit

    UpdateSubmodules ($updateInfo.Updated) $commit

    [string]$assemblyPathRoot = [System.IO.Path]::GetFullPath("$BuildScriptsDirectory..\Build.Tools")
    [string]$mainAssembly = "$assemblyPathRoot\Aderant.Build.dll"

    SetTimeouts
    DownloadPaket $commit
    UpdateMsBuildTasks
    BuildProjects $mainAssembly $isUsingProfile $commit
    LoadAssembly -assemblyPath "$assemblyPathRoot\System.Threading.Tasks.Dataflow.dll"
    LoadAssembly -assemblyPath "$assemblyPathRoot\protobuf-net.dll"
    LoadAssembly -assemblyPath $mainAssembly $true
    LoadLibGit2Sharp $assemblyPathRoot
    LoadVstsTaskLibrary
    SetNuGetProviderPath $assemblyPathRoot
    EnsureClientCertificateAvailable

    # Endpoints are unavailable in the event Initialize-BuildEnvironment is not loaded from the BuildPipeline process.
    $skipEndpointCheck = (Get-Variable -Name 'skipEndpointCheck' -Scope 'Global' -ErrorAction 'SilentlyContinue')
    if ($null -eq $skipEndpointCheck -or -not $skipEndpointCheck) {
        EnsureServiceEndpointsAvailable
    } else {
        Write-Debug 'Service endpoint check skipped.'
    }

    [System.AppDomain]::CurrentDomain.SetData("BuildScriptsDirectory", $BuildScriptsDirectory)
} finally {
    $ErrorActionPreference = $originalErrorActionPreference
}