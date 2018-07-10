param(
    [string]$Repository,
    [string]$Configuration = "Release",
    [string]$Platform = "AnyCPU",
    [bool]$Clean,
    [bool]$LimitBuildWarnings,
    [string]$Flavor,
    [switch]$SkipPackage
)

#[string]$inputRepository = $Repository

#if (-not [string]::IsNullOrWhiteSpace($env:directoryToBuild)) {
#    $Repository = "$env:BUILD_SOURCESDIRECTORY\$env:directoryToBuild"
#    Write-Debug "Directory to build: $Repository"
#}

#$EntryPoint = Get-Variable "BuildTask"
#$global:BuildFlavor = ""

#$DebugPreference = if ($ENV:System_DebugPreference -eq "True") { 'Continue' } else { $DebugPreference }

## The VsTsTaskSdk specifies a prefix of Vsts. Thus commands are renamed from what appears in the source under ps_modules.
## eg Invoke-Tool becomes Invoke-VstsTool

#Enum Result {
#    Succeeded
#    SucceededWithIssues
#    Failed
#    Cancelled
#    Skipped 
#}

#Enum State {
#    Unknown
#    Initialized
#    InProgress
#    Completed
#}

#class LogDetail {
#    [Guid]$id = [Guid]::NewGuid()
#    [datetime]$startTime
#    [datetime]$finishTime
#    [State]$state
#    [Result]$result
#    [bool]$isDesktopBuild

#    LogDetail() {
#    }

#    [void] Start([string]$message) {
#        $this.startTime = [DateTime]::UtcNow
#        $this.state = [State]::InProgress
#        $this.Log($message)
#    }

#    [void] Finish([string]$message, [Result]$result) {
#        $this.finishTime = [DateTime]::UtcNow
#        $this.state = [State]::Completed
#        $this.result = $result
#        $this.Log($message)
#    }

#    [void] Log([string]$message) {
#        $stateText = $this.state.ToString()

#        if ($this.isDesktopBuild) {
#            return
#        }

#        if ($this.state -eq [State]::InProgress) {
#            Write-Host ("##vso[task.logdetail id=$($this.id);type=build;name=$message;order=1;starttime=$($this.startTime);state=$stateText;]")
#            return
#        }

#        if ($this.state -eq [State]::Completed) {
#            $resultText = $this.result.ToString()
#            Write-Host ("##vso[task.logdetail id=$($this.id);finishtime=$($this.finishTime);result=$resultText;state=$stateText]$message")
#        }
#    }
#}

#$MSBuildLocation = ${Env:ProgramFiles(x86)} + "\MSBuild\14.0\Bin\"
#use -Path $MSBuildLocation -Name MSBuild

#$global:IsDesktopBuild = $Env:BUILD_BUILDURI -eq $null
#$global:ToolsDirectory = "$PSScriptRoot\..\Build.Tools"
#[System.Environment]::SetEnvironmentVariable("IsDesktopBuild", $global:IsDesktopBuild, [System.EnvironmentVariableTarget]::Process)

#function GetVssConnection() {
#    try {
#        Write-Host "Creating VSS connection to: $($Env:SYSTEM_TEAMFOUNDATIONSERVERURI)" 
        
#        $assemblyLocation = [Microsoft.VisualStudio.Services.WebApi.VssConnection].Assembly.Location
        
#        Write-Host "Using VSS connection type: $assemblyLocation" 
        
#        return [Microsoft.VisualStudio.Services.WebApi.VssConnection]::new([Uri]::new($Env:SYSTEM_TEAMFOUNDATIONSERVERURI), [Microsoft.VisualStudio.Services.Common.VssCredentials]::new())
#    } catch {
#        Write-Error "Failed to create VSS connection to: $($Env:SYSTEM_TEAMFOUNDATIONSERVERURI)" 
#        throw $_
#    }
#}

#function WarningRatchet() {
#    $destinationBranch = $Env:SYSTEM_PULLREQUEST_TARGETBRANCH
#    $isPullRequest = ![string]::IsNullOrEmpty($destinationBranch)
#    if ($isPullRequest) {
#        Write-Host "Running warning ratchet for $destinationBranch"
#        Import-Module $global:ToolsDirectory\WarningRatchet.dll
#        $result = Invoke-WarningRatchet -TeamFoundationServer $Env:SYSTEM_TEAMFOUNDATIONSERVERURI -TeamProject $Env:SYSTEM_TEAMPROJECT -BuildId $Env:BUILD_BUILDID -DestinationBranchName $destinationBranch  

#        $lastGoodBuildWarningCount = $result.LastGoodBuildCount
#        $currentBuildCount = $result.CurrentBuildCount
#        $ratchet = $result.Ratchet
#        $ratchetRequest = $result.Request

#        if (-not $lastGoodBuildWarningCount) {
#            Write-Host "No last good build found for DefinitionId: $buildDefinitionId"
#        }

#        [int]$lastGoodBuildCount = $lastGoodBuildWarningCount

#        if ($lastGoodBuildWarningCount -ne $null) {
#            Write-Host "The last good build id was: $($ratchetRequest.LastGoodBuild.Id) with $($lastGoodBuildCount) warnings"

#            if ($currentBuildCount -gt $lastGoodBuildCount) {
#                $reporter = $ratchet.GetWarningReporter($ratchetRequest)

#                GenerateAndUploadReport $reporter 

#                [int]$adjustedWarningCount = $reporter.GetAdjustedWarningCount()
#                Write-Output "Adjusted build warnings: $adjustedWarningCount"

#                RenderWarningShields $true $adjustedWarningCount $lastGoodBuildCount
            
#                $permittedWarningsThreshold = 5

#                # Only fail if the adjusted count exceeds the last build
#                if ($adjustedWarningCount -gt $lastGoodBuildCount -and $adjustedWarningCount -gt $permittedWarningsThreshold) {  
                
#                    $sourceBranchName = $Env:BUILD_SOURCEBRANCHNAME

#                    # We always want the master branch to build
#                    if ($sourceBranchName -ne "master") {
#                        throw "Warning count has increased since the last good build"
#                    }
#                }
#                return
#            }
#            RenderWarningShields $false $currentBuildCount $lastGoodBuildCount      
#        }
#    } else {
#        Write-Host "Build is not for a pull request, skipping warning ratchet"
#        RenderWarningShields $false $currentBuildCount $lastGoodBuildCount 
#        return
#    }
#}

#function GenerateAndUploadReport($reporter) {
#    $report = $reporter.CreateWarningReport()
    
#    $stream = [System.IO.StreamWriter] "$env:SYSTEM_DEFAULTWORKINGDIRECTORY\WarningReport.md"
#    $stream.WriteLine($report)
#    $stream.Close()
#    $stream.Dispose()

#    if (-not [string]::IsNullOrWhiteSpace($report)) {
#        Write-Output "##vso[task.uploadsummary]$env:SYSTEM_DEFAULTWORKINGDIRECTORY\WarningReport.md"
#    }
#}

#function RenderWarningShields([bool]$inError, [int]$this, [int]$last) {
#    . $PSScriptRoot\PostWarningShields.ps1 -inError $inError -thisBuild $this -lastBuild $last
#}


## Auto detect build target in debug or release mode by code branch name. If it contains "release" then get into release mode, otherwise debug
## To force build into release or debug mode, set the variable "build.flavor" at TFS build definition, Edit, Variables
## In release mode, the splash screen will remove "(Development)" text and display the current version
#function GetBuildFlavor() {
#    $buildFlavor = ""
#    if ($Env:Build_Flavor) {
#        $buildFlavor = $Env:Build_Flavor
#        Write-Host "....................................................................................................." -ForegroundColor Green
#        Write-Host ".......            Using Build.Flavor from build definition to $buildFlavor          ................" -ForegroundColor Green
#        Write-Host "....................................................................................................." -ForegroundColor Green
#    } elseif ( $Env:Build_SourceBranch -like "*release*" ) {
#        $buildFlavor = "release"
#        Write-Host "....................................................................................................." -ForegroundColor Green
#        Write-Host ".......                           Build in release mode                      ........................" -ForegroundColor Green
#        Write-Host "....................................................................................................." -ForegroundColor Green
#    } else {
#        $buildFlavor = "debug"
#        Write-Host "....... Build in debug mode ................" -foregroundcolor Green
#    }
#    return [string]$buildFlavor;
#}

#=================================================================================================
# Synopsis: Performs a incremental build of the Visual Studio Solution if possible.
# Applies a common build number, executes unit tests and packages the assemblies as a NuGet
# package
#=================================================================================================
task EndToEnd {    
    . $PSScriptRoot\Build-Libraries.ps1

    CompileBuildLibraryAssembly $PSScriptRoot
    LoadLibraryAssembly $PSScriptRoot

    # Import extensibility functions
    Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath 'Functions') -Filter '*.ps1' | ForEach-Object { . $_.FullName }

    Invoke-Build -ModulePath $Env:BUILD_SOURCESDIRECTORY



}

#task PostBuild -Jobs Init, Package, CopyToDrop, {
#}

#task GetDependencies {
#    if (-not $IsDesktopBuild) {
#        # VSTS/TFS agent doesn't always pull the entire repository as it recycles the build directories.
#        # This means the local cached repository might be missing information, so want to always ensure we have full local state
#        # for proper version generation, see bugs:
#        # https://github.com/GitTools/GitVersion/issues/285
#        # https://github.com/GitTools/GitVersion/issues/878
#        # https://github.com/GitTools/GitVersion/issues/993
#        # https://github.com/GitTools/GitVersion/issues/912
#        # Basically VSTS is annoying, like all build systems.
#        & git -C $inputRepository fetch --tags --prune --progress origin

#        . $Env:EXPERT_BUILD_DIRECTORY\Build\LoadDependencies.ps1 -modulesRootPath $Repository
#    }
#}

#task Build {
#    # Get submodules
#    & git submodule update --init --recursive

#    # Don't show the logo and do not allow node reuse so all child nodes are shut down once the master
#    # node has completed build orchestration.
#    $commonArgs = "/nologo /nr:false /m"
#    $commonArgs = "$commonArgs $PSScriptRoot\Aderant.ComboBuild.targets"

#    if (-not $Repository.EndsWith("\")) {
#        $Repository += "\"
#    }

#    if (-not $Env:EXPERT_BUILD_DIRECTORY.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
#        $commonArgs = "$commonArgs /p:EXPERT_BUILD_DIRECTORY=$Env:EXPERT_BUILD_DIRECTORY\"
#    }

# #   $commonArgs = "$commonArgs /p:SolutionRoot=$Repository"
##    $commonArgs = "$commonArgs /p:IsDesktopBuild=$global:IsDesktopBuild"
#  #  $buildFlavor = $Flavor
#   # if ($buildFlavor -eq "") {
#    #    $buildFlavor = GetBuildFlavor   # to build in debug or release
#    #}
    
#    #$global:BuildFlavor = $buildFlavor # to remember and display at the end

#    #$commonArgs = "$commonArgs /p:BuildFlavor=$buildFlavor"    

#    #if ($Clean) {
#    #    $commonArgs = "$commonArgs /p:CleanBin=true"
#    #}

#    #if ($DatabaseBuildPipeline.IsPresent) {
#    #    $commonArgs = "$commonArgs /p:RunDatabaseDeployPipeline=true /p:DropOnFailure=false"
#    #}

#    #if ($Integration.IsPresent) {
#    #    $commonArgs = "$commonArgs /p:RunDesktopIntegrationTests=true"
#    #}

#    #if ($Automation.IsPresent) {
#    #    $commonArgs = "$commonArgs /p:RunDesktopAutomationTests=true"
#    #}

#    if ($ModuleName -ne '') {
#        $commonArgs = "$commonArgs /p:BuildFrom=$ModuleName"
#    } elseif ($global:CurrentModuleName -ne '') {
#        $commonArgs = "$commonArgs /p:BuildFrom=$global:CurrentModuleName"
#    }

#    $commonArgs = "$commonArgs /p:UseSharedDependencyDirectory=false /t:BuildAndPackage"

#    # /p:RunWixToolsOutOfProc=true is required due to this bug with stdout processing
#    # https://connect.microsoft.com/VisualStudio/feedback/details/1286424/
#    $commonArgs = "$commonArgs /p:RunWixToolsOutOfProc=true"

#    try {
#        Push-Location $Repository

#        Import-Module "$PSScriptRoot\..\Profile\Aderant\Aderant.psd1"

#        #TODO - pass this in as a parameter
#        $context = Get-BuildContext
#        $channelId = Publish-BuildContext $context "MSBuild"       

#        $builder = $context.CreateArgumentBuilder("MSBuild")
#        $allArgs = $builder.GetArguments($commonArgs)

#        $commonArgs = [string]::Join(" ", $allArgs)        

#        if ($IsDesktopBuild) {
#            Invoke-Tool -FileName $MSBuildLocation\MSBuild.exe -Arguments $commonArgs -RequireExitCodeZero
#        } else {            
#            . $Env:EXPERT_BUILD_DIRECTORY\Build\InvokeServerBuild.ps1 -Repository $Repository -MSBuildLocation $MSBuildLocation -CommonArgs $commonArgs
#        }    
#    } finally {
#        Pop-Location
#    }
#}

#task BuildCore (job Build -Safe), {
#    # This task always runs after Build

#    # TODO:
#    # http://tfs:8080/tfs/Aderant/ExpertSuite/_apis/test/codeCoverage?buildId=630576&flags=1&api-version=2.0-preview

#    if (-not $IsDesktopBuild) {
#        # We always want to try publish test results as a test failure might be the cause of the build failure and so
#        # we want to see the test results on the TFS dashboard for future analysis
#        $vssConnection = GetVssConnection

#        # Fucking PowerShell. On a desktop OS the implicit conversion to string[] picks "FullName", on the build box it picks "Name" which
#        # fucks everything up as the data that gets piped to the ResultPublisher doesn't have the directory info...so we have to explicit
#        $testResults = gci -Path "$Repository\TestResults" -Filter "*.trx" -Recurse | Select-Object -ExpandProperty FullName

#        if ($testResults) {
#            # Bug in Invoke-ResultPublisher, no one subscribes to LogVerbose which throws a NullReferenceException since there is no null check
#            # before raising the event
#            #$logger = [Microsoft.TeamFoundation.DistributedTask.Task.TestResults.Logger]
#            #$job = Register-ObjectEvent -inputObject $logger -eventName LogVerbose -Action { Write-Verbose $_ }

#            $buildId = $Env:BUILD_BUILDID
#            $buildUri = $Env:BUILD_BUILDURI
#            $owner = $Env:BUILD_REQUESTEDFOR
#            $project = $Env:SYSTEM_TEAMPROJECT

#            Write-Output "Build Number: $buildId"
#            Write-Output "Build Uri: $buildUri"

#            Invoke-ResultPublisher -BuildNumber $buildId -BuildUri $buildUri -Connection $vssConnection -ProjectName $project -resultFiles $testResults -ResultType "Trx" -Owner $owner  #-Configuration -Platform
#        }
#    }

#    # Test for a failure from the Build task and re-throw to fail the build
#    $error = Get-BuildError Build
#    if ($error) {
#        throw $error
#    }
#}

##=================================================================================================
## Synopsis: Does what msbuild/VS can't do consistently.
## Aggressively and recursively deletes all /obj and /bin folders from the build path as well as the output folder.
##=================================================================================================
#task Clean {
#}

#task Test {
#}

#task Quality -If (-not $IsDesktopBuild) {
#    if ($LimitBuildWarnings) {
#        WarningRatchet
#    }
#}

#task CopyToDrop -If (-not $IsDesktopBuild) {
#    if (Test-Path "$($Repository)\CopyToDrop.ps1") {
#        . $Repository\CopyToDrop.ps1
#    }
#}

#task PackageDesktop -If ($global:IsDesktopBuild) {
#    $script:CreatePackage = $true
#}

#task PackageServer -If (-not $global:IsDesktopBuild -and $script:EntryPoint.Value -eq "PostBuild") -Jobs Quality, {
#    $script:CreatePackage = $true
#}

#task Package -If (-not $SkipPackage.IsPresent) -Jobs Init, PackageDesktop, PackageServer, {
#    if ($script:CreatePackage) {
#        Write-Output "Entry point was: $($script:EntryPoint.Value)"

#        . $Env:EXPERT_BUILD_DIRECTORY\Build\Package.ps1 -Repository $Repository
#    }
#}

#task Init {
#    . $Env:EXPERT_BUILD_DIRECTORY\Build\Build-Libraries.ps1
#    CompileBuildLibraryAssembly "$Env:EXPERT_BUILD_DIRECTORY\Build\"
#    LoadLibraryAssembly "$Env:EXPERT_BUILD_DIRECTORY\Build\"

#    Write-Info "Build tree"
#    .\Show-BuildTree.ps1 -File $PSCommandPath

#    Write-Info ("Build Uri:".PadRight(20) + $Env:BUILD_BUILDURI)
#    Write-Info ("Is Desktop Build:".PadRight(20) + $IsDesktopBuild)

#    if (-not $IsDesktopBuild) {
#        # hoho, fucking hilarious
#        # For some reason we cannot load Microsoft assemblies as we get an exception
#        # "Could not load file or assembly 'Microsoft.TeamFoundation.TestManagement.WebApi, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' or one of its dependencies. Strong name validation failed. (Exception from HRESULT: 0x8013141A)
#        # so to work around this we just disable strong-name validation....     
#        cmd /c "`"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6 Tools\x64\sn.exe`" -Vr *,b03f5f7f11d50a3a"
              
#        $global:OnAssemblyResolve = [System.ResolveEventHandler] {
#            param($sender, $e)
#            if ($e.Name -like "*resources*") {
#                return $null
#            }            

#            Write-Host "Resolving $($e.Name)"
            
#            $fileName = $e.Name.Split(",")[0]
#            $fileName = $fileName + ".dll"
        
#            $probeDirectories = @($global:ToolsDirectory, "$Env:AGENT_HOMEDIRECTORY\externals.2.105.7\vstsom", "$Env:AGENT_HOMEDIRECTORY\externals.2.105.7\vstshost", "$Env:AGENT_HOMEDIRECTORY\externals\vstshost", "$Env:AGENT_HOMEDIRECTORY\externals\vstsom", "$Env:AGENT_HOMEDIRECTORY\externals\vstsom", "$Env:AGENT_HOMEDIRECTORY\bin")
#            foreach ($dir in $probeDirectories) {
#                $fullFilePath = "$dir\$fileName"

#                Write-Debug "Probing: $fullFilePath"
                
#                if (Test-Path ($fullFilePath)) {    
#                    Write-Debug "File exists: $fullFilePath"
#                    try {
#                        $a = [System.Reflection.Assembly]::LoadFrom($fullFilePath)
#                        Write-Debug "Loaded dependency: $fullFilePath"
#                        return $a
#                    } catch {
#                        Write-Error "Failed to load $fullFilePath. $_.Exception"
#                    }   
#                } else {
#                    foreach ($a in [System.AppDomain]::CurrentDomain.GetAssemblies()) {
#                        if ($a.FullName -eq $e.Name) {
#                            Write-Debug "Found already loaded match: $a"
#                            return $a
#                        }
#                        if ([System.IO.Path]::GetFileName($a.Location) -eq $fileName) {
#                            Write-Debug "Found already loaded match: $a"
#                            return $a
#                        }
#                    }
#                }
#            }
            
#            Write-Host "Cannot locate $($e.Name). The build will probably fail now."
#            return $null
#        }
        
#        [System.AppDomain]::CurrentDomain.add_AssemblyResolve($global:OnAssemblyResolve)
        
#        Import-Module "$($env:AGENT_HOMEDIRECTORY)\externals\vstshost\Microsoft.TeamFoundation.DistributedTask.Task.LegacySDK.dll"

#        [System.Void][System.Reflection.Assembly]::LoadFrom("$global:ToolsDirectory\Microsoft.VisualStudio.Services.WebApi.dll")
#        [System.Void][System.Reflection.Assembly]::LoadFrom("$global:ToolsDirectory\Microsoft.VisualStudio.Services.Common.dll")
#    }

#    Write-Info "Established build environment"
#}

#function Enter-BuildTask {
#    $script:step = New-Object LogDetail
#    $script:step.isDesktopBuild = $IsDesktopBuild
#    $script:step.Start($Task.Name)
#}


#function Exit-BuildTask {
#    if ($Task.Error) {
#        Write-Verbose "Task `"$($Task.Name)`" has errored!"
#        $script:step.Finish("Done", [Result]::Failed)
#    } else {
#        $script:step.Finish("Done", [Result]::Succeeded)
#    }
#}

task . EndToEnd
