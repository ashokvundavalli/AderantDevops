[string]$BranchRoot = ""
[string]$global:BranchName
[string]$global:BranchLocalDirectory
[string]$global:BranchServerDirectory
[string]$global:BranchModulesDirectory
[string]$global:BranchBinariesDirectory
[string]$global:BranchExpertSourceDirectory
[string]$global:BranchExpertVersion
[string]$global:BranchEnvironmentDirectory
[string]$global:BuildScriptsDirectory
[string]$global:PackageScriptsDirectory
[string]$global:ModuleCreationScripts
[string]$global:ProductManifestPath
[string]$global:CurrentModuleName
[string]$global:CurrentModulePath
[string]$global:CurrentModuleBuildPath
[string[]]$global:LastBuildBuiltModules
[string[]]$global:LastBuildRemainingModules
[string[]]$global:LastBuildGetLocal
[switch]$global:LastBuildGetDependencies
[switch]$global:LastBuildCopyBinaries
[switch]$global:LastBuildDownstream
[switch]$global:LastBuildGetLatest
$global:workspace

$titles = @(
    "Reticulating Splines",
    "Attempting to Lock Back-Buffer",
    "Calculating Inverse Probability Matrices",
    "Compounding Inert Tessellations",
    "Decomposing Singular Values",
    "Dicing Models",
    "Extracting Resources",
    "Obfuscating Quigley Matrix",
    "Fabricating Imaginary Infrastructure",
    "Activating Deviance Threshold",
    "Simulating Program Execution",
    "Abstracting Loading Procedures",
    "Unfolding Helix Packet",
    "Iterating Chaos Array",
    "Calculating Native Restlessness",
    "Filling in the Blanks",
    "Mitigating Time-Stream Discontinuities",
    "Blurring Reality Lines",
	"Reversing the Polarity of the Neutron Flow")

$Host.UI.RawUI.WindowTitle = Get-Random $titles

function Check-DeveloperTools() {

    function Output-VSIXLog {
        $errorsOccurred = $false
        $temp = $env:TEMP
        $lastLogFile = Get-ChildItem $temp | Where { $_.Name.StartsWith("VSIX") } | Sort LastWriteTime | Select -last 1
        if ($lastLogFile -ne $null) {
            $logFileContent = Get-Content $lastLogFile.FullName
            foreach ($line in $logFileContent) {
                if ($line.Contains("Exception")) {
                    $errorsOccurred = $true
                    Write-Host -ForegroundColor Red $line
                    notepad $lastLogFile.FullName
                }
            }
        }
        return $errorsOccurred
    }

    function InstallDeveloperTools() {
        try {
            $vsixFile = gci -Path $ShellContext.BuildToolsDirectory -File -Filter "Aderant.DeveloperTools.vsix" -Recurse | Select-Object -First 1

            if (-not ($vsixFile)) {
                return
            }

            Write-Host "Installing developer tools..."

            $vsixName = "Aderant Developer Tools"
            $vsixId = "b36002e4-cf03-4ed9-9f5c-bf15991e15e4"

            # uninstall the extension
            Write-Host "Uninstalling $vsixName..."

            $vsInstallPath = $env:VS140COMNTOOLS
            $vsix = "$vsInstallPath..\IDE\VSIXInstaller.exe"

            Start-Process -FilePath $vsix -ArgumentList "/q /uninstall:$($vsixId)" -Wait -PassThru | Out-Null

            if ($vsixFile.Exists) {
                Write-Host "Installing VSIX..."

                Start-Process -FilePath $vsix -ArgumentList "/quiet $($vsixFile.FullName)" -Wait -PassThru | Out-Null
                $errorsOccurred = Output-VSIXLog

                if (-not $errorsOccurred) {
                    Write-Host "Updated $($vsixName). Restart Visual Studio for the changes to take effect."
                } else {
                    Write-Host -ForegroundColor Yellow "Something went wrong here. If you open Visual Studio and go to 'Tools -> Extensions and Updates' check if there is the '$vsixName' extension installed and disabled. If so, remove it by hitting 'Uninstall' and try again."
                }
            } else {
                Write-Host -ForegroundColor Yellow "No developer tools VSIX found"
            }
        } catch {
            Write-Host "Exception occured while restoring packages" -ForegroundColor Red
            Write-Host $_ -ForegroundColor Red
        }
    }

	Write-Host
	Write-Host "Detecting developer tools..."

	$extensionsFolder = Join-Path -Path $env:LOCALAPPDATA -ChildPath \Microsoft\VisualStudio\14.0\Extensions\
	$developerTools = Get-ChildItem -Path $extensionsFolder -Recurse -Filter "Aderant.DeveloperTools.dll"
	$version = ""
	$developerTools | ForEach-Object {
		$manifest = Join-Path -Path $_.DirectoryName -ChildPath extension.vsixmanifest
		if (Test-Path $manifest) {
			$manifestContent = Get-Content $manifest
			foreach ($line in $manifestContent) {
				if ($line.Contains('Id="b36002e4-cf03-4ed9-9f5c-bf15991e15e4"')) {
					$match = [System.Text.RegularExpressions.Regex]::Match($line, 'Version="(?<version>[\d\.]+)"')
					$foundVersion = $match.Groups["version"].Value
					if ($foundVersion -gt $version) {
						$version = $foundVersion
					}
				}
			}
		}
	}

	$currentVsixFile = Join-Path -Path $ShellContext.BuildToolsDirectory -ChildPath "Aderant.DeveloperTools.vsix"
	if ($version -eq "") {
		Write-Host -ForegroundColor DarkRed " Aderant Developer Tools for Visual Studio are not installed."
		Write-Host -ForegroundColor DarkRed " If you want them, install them manually via $currentVsixFile"
	} else {
		Write-Host " * Found installed version $version"
	
		if (-not (Test-Path $currentVsixFile)) {
			Write-Host -ForegroundColor Red "Error: could not find file $currentVsixFile"
			return
		}

		[Reflection.Assembly]::Load("System.IO.Compression.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
		$rawFiles = [System.IO.Compression.ZipFile]::OpenRead($currentVsixFile).Entries            
		foreach($rawFile in $rawFiles) {
			if ($rawFile.Name -eq "extension.vsixmanifest") {
				$tempFile = Join-Path -Path $env:TEMP -ChildPath $rawFile.FullName
				[System.IO.Compression.ZipFileExtensions]::ExtractToFile($rawFile, $tempFile, $true)
				$currentManifestContent = Get-Content $tempFile
				foreach ($line in $currentManifestContent) {
					if ($line.Contains('Id="b36002e4-cf03-4ed9-9f5c-bf15991e15e4"')) {
						$match = [System.Text.RegularExpressions.Regex]::Match($line, 'Version="(?<version>[\d\.]+)"')
						$foundVersion = $match.Groups["version"].Value
						Write-Host " * Current version is $foundVersion"
						if ($foundVersion -gt $version) {
							Write-Host
							Write-Host "Updating developer tools..."
							InstallDeveloperTools
						} else {
							Write-Host -ForegroundColor DarkGreen "Your developer tools are up to date."
						}
						break
					}
				}
				break
			}
		}
	}
}

<#
Expert specific variables
#>
$ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentEngine -Value { "C:\AderantExpert\Install\DeploymentEngine.exe" }
$ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentManager -Value { "C:\AderantExpert\Install\DeploymentManager.exe" }

<#
Branch information
#>
function Set-BranchPaths {
    #initialise from default setting
    Write-Debug "Setting information for branch from your defaults"
    $global:BranchLocalDirectory = (GetDefaultValue "DevBranchFolder").ToLower()
    $global:BranchName = ResolveBranchName $global:BranchLocalDirectory
    $global:BranchServerDirectory = (GetDefaultValue "DropRootUNCPath").ToLower()
    $global:BranchModulesDirectory = Join-Path -Path $global:BranchLocalDirectory -ChildPath \Modules
    $global:BranchBinariesDirectory = Join-Path -Path $global:BranchLocalDirectory -ChildPath \Binaries
    $global:BranchEnvironmentDirectory =Join-Path -Path $global:BranchLocalDirectory -ChildPath \Environment

    if ((Test-Path $global:BranchLocalDirectory) -ne $true) {
        Write-Host ""
        Write-Host "*********************************************************************************************************************************"
        Write-Warning "The branch directory does not exist. Call Set-ExpertBranchInfo for initial setup of local directory and branch info"
        Write-Host "*********************************************************************************************************************************"
        Write-Host ""

        throw "Please setup environment"
    }
}

<#
Set-ExpertSourcePath is called on startup and SwitchBranchTo.  It sets $global:BranchExpertVersion and $global:BranchExpertSourceDirectory.
Pre-8.0 environments still use the old folder structure where everything was in the binaries folder, so BranchExpertSourceDirectory is set
according to the setting in the expertmanifest.xml file.
#>
function Set-ExpertSourcePath {
    [xml]$manifest = Get-Content $global:ProductManifestPath
    $global:BranchExpertVersion = $manifest.ProductManifest.ExpertVersion

    if ($global:BranchExpertVersion.StartsWith("8")) {
        $global:BranchExpertSourceDirectory = Join-Path -Path $global:BranchLocalDirectory -ChildPath \Binaries\ExpertSource
        if ((Test-Path $global:BranchExpertSourceDirectory) -ne $true){
            [System.IO.Directory]::CreateDirectory($global:BranchExpertSourceDirectory) | Out-Null
        }
    } else {
        $global:BranchExpertSourceDirectory = $global:BranchBinariesDirectory
    }
}

function Set-ScriptPaths {

    if ([System.IO.File]::Exists("$global:BranchModulesDirectory\ExpertManifest.xml")) {
        $root = Resolve-Path "$PSScriptRoot\..\..\..\"

        $global:BuildScriptsDirectory = Join-Path -Path $root -ChildPath Src\Build
        $global:PackageScriptsDirectory = Join-Path -Path $root -ChildPath Src\Package
        $global:ModuleCreationScripts = Join-Path -Path $root -ChildPath Src\ModuleCreator
        $global:ProductManifestPath = Join-Path -Path $global:BranchModulesDirectory -ChildPath ExpertManifest.xml
    } else {
        $global:BuildScriptsDirectory = Join-Path -Path $global:BranchModulesDirectory -ChildPath \Build.Infrastructure\Src\Build
        $global:PackageScriptsDirectory = Join-Path -Path $global:BranchModulesDirectory -ChildPath \Build.Infrastructure\Src\Package
        $global:ModuleCreationScripts = Join-Path -Path $global:BranchModulesDirectory -ChildPath \Build.Infrastructure\Src\ModuleCreator
        $global:ProductManifestPath = Join-Path -Path $global:PackageScriptsDirectory -ChildPath \ExpertManifest.xml
    }
}

<#
    Initialise functions from Build-Libraries.ps1
#>
function Initialise-BuildLibraries {
    invoke-expression "$BuildScriptsDirectory\Build-Libraries.ps1"
}

function ResolveBranchName($branchPath){
    if (IsMainBanch $branchPath){
        $name = "MAIN"
    } elseif (IsDevBanch $branchPath){
        $name = $branchPath.Substring($branchPath.LastIndexOf("dev\", [System.StringComparison]::OrdinalIgnoreCase))
    } elseif (IsReleaseBanch $branchPath){
        $name = $branchPath.Substring($branchPath.LastIndexOf("releases\", [System.StringComparison]::OrdinalIgnoreCase))
    }
    return $name
}

function IsDevBanch([string]$name){
    return $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase) -and $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase)
}

function IsReleaseBanch([string]$name){
    return $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -and $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase)
}

function IsMainBanch([string]$name){
    return $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -and $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase)
}

# Called from SwitchBranchTo
function Set-ChangedBranchPaths([string]$name){
    #initialise from default setting
    Write-Host "Change branch to $name"

    # container as in dev or release
    $newBranchContainer = ""
    $previousBranchContainer = ""

    # name of branch or MAIN
    $newBranchName = ""
    $previousBranchName = ""

    #was the pervious branch MAIN?
    [bool]$changeToContainerFromMAIN = $false

    # get the new and previous name a container parts
    if ((IsDevBanch $global:BranchName) -or (IsReleaseBanch $global:BranchName)) {
        $previousBranchContainer = $global:BranchName.Substring(0,$global:BranchName.LastIndexOf("\"))
        $previousBranchName = $global:BranchName.Substring($global:BranchName.LastIndexOf("\")+1)
    }elseif((IsMainBanch $global:BranchName)){
        $previousBranchName = "MAIN"
        $changeToContainerFromMAIN = $true
    }

    if ((IsDevBanch $name) -or (IsReleaseBanch $name)){
        $newBranchContainer = $name.Substring(0,$name.LastIndexOf("\"))
        $newBranchName = $name.Substring($name.LastIndexOf("\")+1)
    } elseif ((IsMainBanch $name)){
        $newBranchName = "MAIN"
        $newBranchContainer = "\"
    }

    $success = $false
    if ($changeToContainerFromMAIN){
        $success = Switch-BranchFromMAINToContainer $newBranchContainer $previousBranchContainer $newBranchName $previousBranchName
    } else {
        $success = Switch-BranchFromContainer $newBranchContainer $previousBranchContainer $newBranchName $previousBranchName
    }

    if ($success -eq $false) {
        Write-Host -ForegroundColor Yellow "'$name' branch was not found on this machine."
        return $false
    }

    #Set common paths
    $global:BranchModulesDirectory = (Join-Path -Path $global:BranchLocalDirectory -ChildPath "Modules" )

    $global:BranchBinariesDirectory = (Join-Path -Path $global:BranchLocalDirectory -ChildPath "Binaries" )
    if ((Test-Path $global:BranchBinariesDirectory) -eq $false){
        New-Item -Path $global:BranchBinariesDirectory -ItemType Directory
    }

    return $true
}

<#
 we need to cater for the fact MAIN is the only branch and not a container like dev or release
#>
function Switch-BranchFromMAINToContainer($newBranchContainer, $previousBranchContainer, $newBranchName, $previousBranchName) {
    #change name and then container and remove extra backslash's
    $globalBranchName = ($global:BranchName -replace $previousBranchName,$newBranchName)
    $globalBranchName = $newBranchContainer+"\"+$globalBranchName

    if ($globalBranchName -eq "\") {
        return $false
    }

    #strip MAIN then add container and name
    $globalBranchLocalDirectory = $global:BranchLocalDirectory.Substring(0,$global:BranchLocalDirectory.LastIndexOf("\")+1)
    $globalBranchLocalDirectory = (Join-Path -Path $globalBranchLocalDirectory -ChildPath( Join-Path  -Path $newBranchContainer -ChildPath $newBranchName))

    if ((Test-Path $globalBranchLocalDirectory) -eq $false -or $globalBranchLocalDirectory.EndsWith("ExpertSuite")) {
        return $false
    }

    $global:BranchName = $globalBranchName
    $global:BranchLocalDirectory = $globalBranchLocalDirectory

    #strip MAIN then add container and name
    $global:BranchServerDirectory = $global:BranchServerDirectory.Substring(0,$global:BranchServerDirectory.LastIndexOf("\")+1)
    $global:BranchServerDirectory = (Join-Path -Path $global:BranchServerDirectory -ChildPath( Join-Path  -Path $newBranchContainer -ChildPath $newBranchName))

    $global:BranchServerDirectory = [System.IO.Path]::GetFullPath($global:BranchServerDirectory)

    return $true
}

<#
 we dont have to do anything special if we change from a container to other branch type
#>
function Switch-BranchFromContainer($newBranchContainer, $previousBranchContainer, $newBranchName, $previousBranchName){
    #change name and then container and remove extra backslash's
    $globalBranchName = $global:BranchName.replace($previousBranchName,$newBranchName)
    $globalBranchName = $globalBranchName.replace($previousBranchContainer,$newBranchContainer)
    if(IsMainBanch $globalBranchName){
        $globalBranchName = [System.Text.RegularExpressions.Regex]::Replace($globalBranchName,"[^1-9a-zA-Z_\+]","");
    }

    if ($globalBranchName -eq "\") {
        return $false
    }

    $globalBranchLocalDirectory = $global:BranchLocalDirectory.Substring(0, $global:BranchLocalDirectory.LastIndexOf($previousBranchContainer));
    $globalBranchLocalDirectory = (Join-Path -Path $globalBranchLocalDirectory -ChildPath( Join-Path -Path $newBranchContainer -ChildPath $newBranchName))

    if ((Test-Path $globalBranchLocalDirectory) -eq $false -or $globalBranchLocalDirectory.EndsWith("ExpertSuite")) {
        return $false
    }

    $global:BranchName = $globalBranchName
    $global:BranchLocalDirectory = $globalBranchLocalDirectory

    $global:BranchServerDirectory = $global:BranchServerDirectory.Substring(0, $global:BranchServerDirectory.LastIndexOf($previousBranchContainer));
    $global:BranchServerDirectory = (Resolve-Path -Path ($global:BranchServerDirectory + $newBranchContainer + "\" + $newBranchName)).ProviderPath

    $global:BranchServerDirectory = [System.IO.Path]::GetFullPath($global:BranchServerDirectory)

    return $true
}

function Set-CurrentModule($name, [switch]$quiet){
    if (!($name)) {
        if(!($global:CurrentModuleName)){
            Write-Warning "No current module is set"
            return
        } else {
            Write-Host "The current module is [$global:CurrentModuleName] on the branch [$global:BranchName]"
            return
        }
    }
    if (-not ($quiet)) {
        Write-Host "Setting information for the module $name"
    }

    if ($name -eq ".") {
        $name = Resolve-Path $name
    }

    if ([System.IO.Path]::IsPathRooted($name)) {
        if (IsGitRepository $name) {
            SetRepository $name
            Set-Location $name
            return
        }
    }

    $global:CurrentModuleName = $name

    Write-Debug "Current module [$global:CurrentModuleName]"
    $global:CurrentModulePath = Join-Path -Path $global:BranchModulesDirectory -ChildPath $global:CurrentModuleName

    if((Test-Path $global:CurrentModulePath) -eq $false){
        Write-Warning "the module [$global:CurrentModuleName] does not exist, please check the spelling."
        $global:CurrentModuleName = ""
        $global:CurrentModulePath = ""
        return
    }

    Write-Debug "Current module path [$global:CurrentModulePath]"
    $global:CurrentModuleBuildPath = Join-Path -Path $global:CurrentModulePath -ChildPath \Build

    $ShellContext.IsGitRepository = $false
}

function IsGitRepository([string]$path) {
    return @(gci -path $path -Filter ".git" -Recurse -Depth 1 -Attributes Hidden -Directory).Length -gt 0
}

function SetRepository([string]$path) {
    Write-Debug "Setting repository: $path"
    Import-Module $PSScriptRoot\AderantGit.psm1

    $global:CurrentModulePath = $path
    $global:CurrentModuleName = ([System.IO.DirectoryInfo]$global:CurrentModulePath).Name
    $ShellContext.IsGitRepository = $true
}

function Get-CurrentModule() {
    return Get-ExpertModule -ModuleName $global:CurrentModuleName
}

<#
.Synopsis
    Starts dbgen.exe. This is very similar to Update-Database -interactive. You may want to use that instead.
.Description
    Starts dbgen.exe found in your expert source directory. This is very similar (but inferior) to Update-Database -interactive. You might want to use that instead.
#>
function Start-dbgen() {
    #Normally found at: C:\CMS.NET\Bin\dbgen.exe
    $dbgen = [System.IO.Path]::Combine($global:BranchExpertSourceDirectory, "dbgen\dbgen.exe")
    Invoke-Expression $dbgen
}

function OutputEnvironmentDetails {
    Write-Host ""
    Write-Host "-----------------------------"
    Write-Host "Local Branch Information"
    Write-Host "-----------------------------"
    Write-Host "Name :" $global:BranchName
    Write-Host "Path :" $global:BranchLocalDirectory
    Write-Host ""
    Write-Host "-----------------------------"
    Write-Host "Server Branch Information"
    Write-Host "-----------------------------"
    Write-Host "Path :" $global:BranchServerDirectory
    Write-Host ""
    Write-Host "-----------------------------"
    Write-Host "Current Module Information"
    Write-Host "-----------------------------"
    Write-Host "Name :" $global:CurrentModuleName
    Write-Host "Path :" $global:CurrentModulePath
    Write-Host ""
}

<#
 gets the folder the current script is executing from
 #>
function Get-ScriptFolder {
     return Split-Path (Get-Variable MyInvocation).Value.ScriptName
}

<#
 return the connection string to be used for the sitemap builder
#>
function Get-SystemMapConnectionString {
    return (GetDefaultValue "systemMapConnectionString").ToLower()
}

function New-BuildModule($name){
    $shell = ".\create_module.ps1 -ModuleName $name -DestinationFolder $global:BranchModulesDirectory"
    pushd $global:ModuleCreationScripts
    invoke-expression $shell
    popd
}

<#
.Synopsis
    Runs dbprepare.exe on the specified database. Defaults to version 81.
.Description
    Runs dbprepare.exe on the specified database. Defaults to version 81.
.PARAMETER database
    MANDATORY: The name of the database to use. 
.PARAMETER saPassword
	MANDATORY: The sa user password for the database server.
.PARAMETER server
	The name of the database server. Defaults to the local machine name.
.PARAMETER instance
	The database serverinstance that the database resides on.
.PARAMETER version
	The version to prep the database to. Defaults to 81.
.PARAMETER interactive
	Starts DBPREPARE in interactive mode
#>
function Prepare-Database (
        [Parameter(Mandatory=$true)]
		[string]$database,
        [Parameter(Mandatory=$true)]
        [string]$saPassword,
        [string]$server = $env:COMPUTERNAME,
        [string]$instance,
		[string]$version = "81",
        [switch]$interactive
        ) {

    [string]$cmsConnection
    if (-Not [string]::IsNullOrWhiteSpace($instance)) {
		$instance = "\$instance"    
		$cmsConnection = "[$server]`r`nVendor=1`r`nLocation=$server$instance`r`nDatabase=$database`r`nTitle=$server.$database`r`nForceManualLogin=0"
        Write-Debug "Running dbprepare against database: $database on server: $server$instance"
    } else {
        $cmsConnection = "[$server]`r`nVendor=1`r`nLocation=$server`r`nDatabase=$database`r`nTitle=$server.$database`r`nForceManualLogin=0"
        Write-Debug "Running dbprepare against database: $database on server: $server"
    }

    # Checks if CMS.INI is present in %APPDATA%\Aderant and create it if not. Appends connection details to CMS.INI if they are not present.
    $cmsPath = "$env:APPDATA\Aderant\CMS.INI"
    if (Test-Path $cmsPath) {
        if ((Select-String -InputObject (Get-Content $cmsPath | Out-String) -SimpleMatch "[$server]") -eq $null) {
            Write-Debug "Adding $server connection to existing CMS.INI file in %APPDATA%\Aderant."
            Add-Content $cmsPath "`r`n$cmsConnection"
        } else {
            # If there is an existing connection with incorrect data, dbprepare will open and wait for user input.
            Write-Debug "$server connection present in existing CMS.INI file in %APPDATA%\Aderant."
        }
	} else {
        Write-Debug "Creating CMS.INI file in %APPDATA%\Aderant with $server connection."
        New-Item $cmsPath -ItemType File -Value $cmsConnection
    }

    $dbPreparePath = "$global:BranchExpertSourceDirectory\dbgen\dbprepare.exe"
    $dbPrepareArgs = "target=$server$instance.$database autostart=1 autoclose=1 installshield=1 login=SA password=`"$saPassword`" ERRORLOG=`"$global:BranchBinariesDirectory\Logs\dbprep.log`" version=$version prep=database,other"

    if (-not $interactive.IsPresent) {
        $dbPrepareArgs = $dbPrepareArgs + " hide=1"
    }

    Write-Host "Starting dbprepare.exe from: $dbPreparePath"
    Write-Host "dbprepare.exe arguments: $dbPrepareArgs"

    Start-Process -FilePath $dbPreparePath -ArgumentList $dbPrepareArgs -Wait -PassThru | Out-Null
}

<#
.Synopsis
    Deploys the database project to your database defined in the environment manifest
.Description
    Deploys the database project, thereby updating your database to the correct definition.
.PARAMETER interactive
    Starts DBGEN in interactive mode
#>
function Update-Database([string]$manifestName, [switch]$interactive) {
    [string]$fullManifest = ''

	Write-Warning "The 'upd' command is currently unavailable. Please use DBGen for now to update your database."

	return

    if ($global:BranchExpertVersion.StartsWith("8")) {
        $fullManifest = Join-Path -Path $global:BranchBinariesDirectory -ChildPath 'environment.xml'
    } else {
        if  ($manifestName -eq $null) {
            write "Usage: Update-BranchDatabase <manifestName>"
            return
        } else {
            $fullManifest = Join-Path -Path $global:BranchEnvironmentDirectory -ChildPath "\$manifestName.environment.xml"
        }
    }


    if (Test-Path $fullManifest) {
        Write-Debug "Using manifest: $fullManifest"

        # Update the DBGEN defaults for development churn
        [Xml]$manifest = Get-Content $fullManifest
        $server = $manifest.environment.expertDatabaseServer.serverName
        $db = $manifest.environment.expertDatabaseServer.databaseConnection.databaseName

        $query = @"
begin tran
update CMS_DB_OPTION set OPTION_VALUE = '{0}', LAST_MODIFIED = getdate()
where OPTION_CODE in ('PERMIT_NULLLOSS', 'PERMIT_DATALOSS')
commit
"@
        # set PERMIT_NULLLOSS and PERMIT_DATALOSS to true
        $command = "sqlcmd -S $server -d $db -E -Q `"" + [string]::Format($query, "Y") + "`""
        Invoke-Expression $command

        $shell = "powershell -NoProfile -NoLogo `"$global:PackageScriptsDirectory\DeployDatabase.ps1`" -environmentManifestPath `"$fullManifest`" -expertSourceDirectory `"$global:BranchExpertSourceDirectory`" -interactive:$" + $interactive
        # Invoke-Expression falls on its face here due to a bug with [switch] - if used the switch argument cannot be converted to a switch parameter
        # which is very annoying
        # http://connect.microsoft.com/PowerShell/feedback/details/742084/powershell-v2-powershell-cant-convert-false-into-swtich-when-using-file-param
        cmd /c $shell

        # reset PERMIT_NULLLOSS and PERMIT_DATALOSS to false
        $command = "sqlcmd -S $server -d $db -E -Q `"" + [string]::Format($query, "N") + "`""
        Invoke-Expression $command
    } else {
        Write-Error "No manifest specified at path: $fullManifest"
    }
}

<#
.Synopsis
    Installs the latest version of the Software Factory
.Description
    Will uninstall the previous vsix and then install the latest version from the drop location
#>
function Install-LatestSoftwareFactory([switch]$local) {
    # Paket-TODO: don't perfom this check now, revisit this if necessary when we have successfully switched to Paket
    # (the current check is invalid anyway as it check a wrong SW Factory version in the wrong VS version)
    #if (Check-LatestSoftwareFactory) {
    #    Write-Host "No update necessary, you have the latest version"
    #} else {
        if ($local) {
            Install-LatestVisualStudioExtension -module "Libraries.SoftwareFactory" -local
        }
        else {
            Install-LatestVisualStudioExtension -module "Libraries.SoftwareFactory"
        }
    #}
}

<#
.Synopsis
    Installs the latest version of the given module
.Description
    Will uninstall the previous vsix and then install the latest version from the drop location
.Example
    Install-LatestVisualStudioExtension SDK.Database
    Will install the latest version of the SDK.Database project
#>
function Install-LatestVisualStudioExtension(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [ValidateSet('SDK.Database', 'Libraries.SoftwareFactory')]
        [String]$module,
        [switch]$local) {

    $installDetails = $null

    if ($module -eq "SDK.Database") {
        $info = @{}
        $info.ProductManifestName = "SDK.Database"
        $info.ExtensionDisplayName = "Aderant Expert Database Project"
        $info.ExtensionName = "Aderant.Database.Project.1b755e8f-3540-4935-b959-e4d33f247bd2"
        $info.ExtensionFile = "Aderant.Database.ProjectSystem.vsix"
        $installDetails = New-Object –TypeName PSObject –Prop $info
    }

    if ($module -eq "Libraries.SoftwareFactory") {
        $info = @{}
        $info.ProductManifestName = "SoftwareFactory"
        $info.ExtensionDisplayName = "Aderant Software Factory"
        $info.ExtensionName = "Aderant.SoftwareFactory"
        $info.ExtensionFile = "AderantSoftwareFactory.vsix"
        $installDetails = New-Object –TypeName PSObject –Prop $info
    }

    if ($local) {
        Install-LatestVisualStudioExtensionImpl $installDetails -local
    }
    else {
        Install-LatestVisualStudioExtensionImpl $installDetails
    }
}

function Install-LatestVisualStudioExtensionImpl($installDetails, [switch]$local) {

    # uninstall the extension
    Write-Host "Uninstalling $($installDetails.ProductManifestName)..."
    $vsix = "VSIXInstaller.exe"
    Start-Process -FilePath $vsix -ArgumentList "/q /uninstall:$($info.ExtensionName)" -Wait -PassThru | Out-Null
    Start-Process -FilePath $vsix -ArgumentList "/q /uninstall:$($info.ExtensionName)" -Wait -PassThru | Out-Null
    Start-Process -FilePath $vsix -ArgumentList "/q /uninstall:$($info.ExtensionName)" -Wait -PassThru | Out-Null

    # take VSIX out of local source directory
    if ($local) {
        Write-Host "Attempting to install $($info.ProductManifestName) from local source directory."
        $vsixFile = [System.IO.Path]::Combine($global:BranchExpertSourceDirectory, $info.ExtensionFile)
    }

    # take VSIX from drop folder
    else {
        Write-Host "Attempting to install $($info.ProductManifestName) from drop folder."
        $localInstallDirectory = [System.IO.Path]::Combine($global:BranchLocalDirectory, $info.ProductManifestName + ".Install")

        [xml]$manifest = Get-Content $global:ProductManifestPath
        [System.Xml.XmlNode]$module = $manifest.ProductManifest.Modules.SelectNodes("Module") | Where-Object{ $_.Name.Contains($info.ProductManifestName)}

        Invoke-Expression "$BuildScriptsDirectory\Build-Libraries.ps1"
        $dropPathVSIX = (GetPathToBinaries $module $global:BranchServerDirectory)

        if (!(Test-Path $localInstallDirectory)) {
            New-Item $localInstallDirectory -ItemType directory
        } else {
            DeleteContentsFrom $localInstallDirectory
        }

        CopyContents -copyFrom $dropPathVSIX -copyTo $localInstallDirectory

        $vsixFile = [System.IO.Path]::Combine($localInstallDirectory, $info.ExtensionFile)
    }

    Write-Host $vsixFile

    if ([System.IO.File]::Exists($vsixFile)) {
        $lastWriteTime = (Get-ChildItem $vsixFile).LastWriteTime
        Write-Host "VSIX updated on $lastWriteTime"
        Write-Host "Installing $($info.ProductManifestPathName). Please wait..."
        Start-Process -FilePath $vsix -ArgumentList "/quiet $vsixFile" -Wait -PassThru | Out-Null
        $errorsOccurred = Output-VSIXLog
        if (-not $errorsOccurred) {
        Write-Host "Updated $($info.ProductManifestPathName). Restart Visual Studio for the changes to take effect."
        } else {
            Write-Host
            $displayName = $info.ExtensionDisplayName
            Write-Host -ForegroundColor Yellow "Something went wrong here. If you open Visual Studio and go to 'TOOLS -> Exensions and Updates' check if there is the '$displayName' extension installed and disabled. If so, remove it by hitting 'Uninstall' and try this command again."
        }
    }
}

Function Output-VSIXLog {
    $errorsOccurred = $false
    $temp = $env:TEMP
    $lastLogFile = Get-ChildItem $temp | Where { $_.Name.StartsWith("VSIX") } | Sort LastWriteTime | Select -last 1
    if ($lastLogFile -ne $null) {
        $logFileContent = Get-Content $lastLogFile.FullName
        foreach ($line in $logFileContent) {
            if ($line.Contains("Exception")) {
                $errorsOccurred = $true
                Write-Host -ForegroundColor Red $line
                notepad $lastLogFile.FullName
            }
        }
    }
    return $errorsOccurred
}

# builds the current module using default parameters
function Start-BuildForCurrentModule([string]$clean, [bool]$debug, [bool]$release) {
    # Parameter must be a string as we are shelling out which we can't pass [switch] to
    $shell = ".\BuildModule.ps1 -moduleToBuildPath $global:CurrentModulePath -dropRoot $global:BranchServerDirectory -cleanBin $clean"

    if ($debug) {
        $shell += " -debug"
    } elseif ($release) {
		$shell += " -release"
	}

    pushd $global:BuildScriptsDirectory
    invoke-expression $shell
    popd
}

<#
.Synopsis
    Retrieves the dependencies required to build the current module
#>
function Get-DependenciesForCurrentModule([switch]$noUpdate, [switch]$showOutdated, [switch]$force) {
    if ([string]::IsNullOrEmpty($global:CurrentModulePath)) {
        Write-Warning "The current module is not set so the binaries will not be copied"
    } else {
        try {
            pushd $global:BuildScriptsDirectory
            .\LoadDependencies.ps1 -modulesRootPath $global:CurrentModulePath -dropPath $global:BranchServerDirectory -update:(-not $noUpdate) -showOutdated:$showOutdated -force:$force
        } finally {
            popd
        }
    }
}

# gets dependencies for each module using default parameters
function Get-DependenciesForEachModule {
    Get-ExpertModules | % {
        if (-not ($_.Name.StartsWith("ThirdParty.")) `
            -and -not ($_.Name.StartsWith("ThirdParty.")) `
            -and -not ($_.Name -eq "Build.T4Task") `
            -and -not ($_.Name -eq "Thirdparty.ExifJS") `
            -and -not ($_.Name -eq "Thirdparty.Microsoft.Office.Interop") `
            -and -not ($_.Name -eq "Marketing.Help") `
            -and -not ($_.Name -eq "Tests.UIAutomation") `
            -and -not ($_.Name -eq "UIAutomation.Framework") `
            -and -not ($_.Name -eq "Expert.Help") `
            -and -not ($_.Name -eq "Installs.Marketing") `
            -and -not ($_.Name -eq "Internal.Licensing")) {
            Write-Host "Getting dependencies for $_..."
            cm $_.Name; gd
        }
    }
}


# gets dependencies for current module using default parameters
function Get-LocalDependenciesForCurrentModule {
    if (Test-Path $global:BuildScriptsDirectory\Load-LocalDependencies.ps1) {
        $shell = ".\Load-LocalDependencies.ps1 -moduleName $global:CurrentModuleName -localModulesRootPath $global:BranchModulesDirectory -serverRootPath $global:BranchServerDirectory"
        try {
            pushd $global:BuildScriptsDirectory
            invoke-expression $shell
        } finally {
            popd
        }
    }
}

function Copy-BinariesFromCurrentModule() {
    if ([string]::IsNullOrEmpty($global:CurrentModulePath)) {
        Write-Warning "The current module is not set so the binaries will not be copied"
    } else {
        Initialise-BuildLibraries

        pushd $global:BuildScriptsDirectory
        ResolveAndCopyUniqueBinModuleContent -modulePath $global:CurrentModulePath -copyToDirectory $global:BranchExpertSourceDirectory -suppressUniqueCheck $true
        popd
    }    
}

<#
.Synopsis
    Runs a GetProduct for the current branch
.Description
    Uses the expertmanifest from the local Build.Infrastructure\Src\Package directory.
    This will always return the pdb's.
    The binaries will be loaded into your branch binaries directory. e.g. <your_branch_source>\Binaries
.PARAMETER onlyUpdated
    Switch to indicate that only updated modules should get pulled in.
.PARAMETER createBackup
    Switch to create a backup of the Binaries folder (named BinariesBackup in the same folder) after successfully retrieving the product.
    This is intended to be used by developers who call Copy-BinariesFromCurrentModules (cb) or Copy-BinToEnvironment and want to have a backup with the original files from the Get-Product call.
.EXAMPLE
    Get-Product -createBackup
#>
function Get-Product ([switch]$onlyUpdated, [switch]$createBackup) {
    $buildInfrastructure = $global:PackageScriptsDirectory.Replace("Package", "")

    & tf.exe vc "get" $global:ProductManifestPath

    pushd $global:PackageScriptsDirectory
    & .\GetProduct.ps1 -ProductManifestPath $global:ProductManifestPath -dropRoot $global:BranchServerDirectory -binariesDirectory $global:BranchBinariesDirectory -getDebugFiles 1 -systemMapConnectionString (Get-SystemMapConnectionString) -onlyUpdated:$onlyUpdated.ToBool()
    popd

    if ($createBackup) {
        Write-Host "Creating backup of Binaries folder."
        $backupPath = $global:BranchLocalDirectory + "\BinariesBackup"
        if (-not (Test-Path $backupPath)) {
            New-Item -ItemType Directory -Path $backupPath
        }
        Invoke-Expression "robocopy.exe $global:BranchBinariesDirectory $backupPath /MIR /SEC /TEE /R:2 /XD $global:BranchBinariesDirectory\ExpertSource\Customization" | out-null
        Write-Host "Backup complete."
    }
}


<#
.Synopsis
    Runs a GetProduct for the current branch but will not contain the pdb's
.Description
    Uses the expertmanifest from the local Build.Infrastructure\Src\Package directory.
    No pdb's returned
    The binaries will be loaded into your branch binaries directory. e.g. <your_branch_source>\Binaries
#>
function Get-ProductNoDebugFiles {
    $shell = ".\GetProduct.ps1 -ProductManifestPathPath $global:ProductManifestPath -dropRoot $global:BranchServerDirectory -binariesDirectory $global:BranchBinariesDirectory -systemMapConnectionString (Get-SystemMapConnectionString)"
    pushd $global:PackageScriptsDirectory
    invoke-expression $shell | Out-Host
    popd
}

<#
.Synopsis 
    Displays the BuildAll version in the binaries directory of the current branch.
.Description
    WARNING: If you have done a Get-Product or Get-ProductZip since your last deployment, then it will show the version number of the Get-ProductZip rather than what is deployed.
.PARAMETER copyToClipboard
    If specified the BuildAll version will be copied to the clipboard.
#>
function Get-ProductBuild([switch]$copyToClipboard) {
    $versionFilePath = "$global:BranchBinariesDirectory\BuildAllZipVersion.txt"
    if ([System.IO.File]::Exists($versionFilePath)) {
		if ((Get-Content -Path $versionFilePath) -match "[^\\]*[\w.]BuildAll_[\w.]*[^\\]") {
			Write-Host "Current BuildAll version in $global:BranchName` branch:`r`n"
			Write-Host $Matches[0]
			if ($copyToClipboard) {
				Add-Type -AssemblyName "System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
				[System.Windows.Forms.Clipboard]::SetText($Matches[0])
			}
		} else {
			Write-Error "Content of BuildAllZipVersion.txt is questionable."
		}
    } else {
        Write-Error "No BuildAllZipVersion.txt present in $global:BranchBinariesDirectory."
    }
}

<#
.Synopsis
    Gets the latest product zip from the BuildAll output and unzips to your BranchBinariesDirectory
.Description
    The binaries will be loaded into your branch binaries directory. e.g. <your_branch_source>\Binaries
#>
function Get-ProductZip {
    Write-Host "Getting latest product zip from [$BranchServerDirectory]"
    $zipName = "ExpertBinaries.zip"
    [string]$pathToZip = (PathToLatestSuccessfulPackage -pathToPackages $BranchServerDirectory -packageZipName $zipName)

    Write-Host "Selected " $pathToZip

    $pathToZip = $pathToZip.Trim()
    DeleteContentsFromExcludingFile -directory $BranchBinariesDirectory "environment.xml"
    Copy-Item -Path $pathToZip -Destination $BranchBinariesDirectory
    $localZip =  (Join-Path $BranchBinariesDirectory $zipName)
    Write-Host "About to extract zip to [$BranchBinariesDirectory]"
	
	$zipExe = join-path $ShellContext.BuildToolsDirectory "\7z.exe"
	if (test-path $zipExe) {
		$SourceFile=$localZip
		$Destination=$BranchBinariesDirectory
		&$zipExe x $SourceFile "-o$Destination" -y
	} else {
		Write-Host "Falling back to using Windows zip util as 7-zip does not exist on this system"
		$shellApplication = new-object -com shell.application
		$zipPackage = $shellApplication.NameSpace($localZip)
		$destinationFolder = $shellApplication.NameSpace($global:BranchBinariesDirectory)
		$destinationFolder.CopyHere($zipPackage.Items())	 
	}
	
    Write-Host "Finished extracting zip"
    [string]$versionFilePath = Join-Path $global:BranchBinariesDirectory "BuildAllZipVersion.txt"
    echo $pathToZip | Out-File -FilePath $versionFilePath
}

<#
.Synopsis
    Builds a list of modules
.Description
    Automatically orders the modules according to their dependencies. Automatically handles
    copying dependencies between the list of modules. Use -getDependencies $true and -copyBinaries $true
    to get dependencies before local dependency management and starting each module build and/or to copy
    the output to the binaries location.
.PARAMETER workflowModuleNames
    An array of workflow module names
.PARAMETER changeset
    If specified will build all modules in the current changeset. This overrides workflowModuleNames
.PARAMETER getDependencies
    If specified, will call get-depedencies before copying the output from any other specified modules already built
    and running the build of this module.
.PARAMETER copyBinaries
    If specified, will copy the output of each module build to the binaries location.
.PARAMETER downstream
    If specified will build the sepcified modules and any modules which depend on them.
.PARAMETER getLatest
    If specified will get the latest source for the module from TFS before building.
.PARAMETER continue
    If specified will continue the last build starting at a build for the last module that failed
.PARAMETER getLocal
    If specified will get this comma delimited list of dependencies locally instead of from the drop folder
.PARAMETER exclude
    If specified will exclude this comma delimited list of modules from the build
.PARAMETER skipUntil
    A module name that if specified will build the list of modules as normal but skip the ones before the specified module
.EXAMPLE
        Build-ExpertModules
    Will build the current module. No dependencies will be prefetched and the output will not be copied to the binaries folder
.EXAMPLE
        Build-ExpertModules Libraries.Workflow
    Will build the "Libraries.Workflow" module. No dependencies will be prefetched and the output will not be copied to the binaries folder
.EXAMPLE
        Build-ExpertModules Libraries.Workflow, Libraries.Foundation, Libraries.Presentation
    Will build the specified modules in the correct order according to their dependencies (Libraries.Foundation, Libraries.Presentation, Libraries.Workflow).
    The output of each modules will be copied to the dependencies folder of the others before they are built, if are dependent.
    No dependencies will be prefetched and the output will not be copied to the binaries folder
.EXAMPLE
        Build-ExpertModules -changeset
    Will build the modules which have files currently checked out for edit. No dependencies will be prefetched and the output will not be copied to the binaries folder
.EXAMPLE
        Build-ExpertModules Libraries.Foundation -getLatest
    Will build the modules specified after get the latest source from TFS. No dependencies will be prefetched and the output will not be copied to the binaries folder
.EXAMPLE
        Build-ExpertModules Libraries.Foundation -getDependencies -copyBinaries -downstream
    Will build the specified module and any modules which directly or indirectly depend on it.
    The dependencies will be fetched before building and the output will be copied to the binaries folder.
.EXAMPLE
        Build-ExpertModules Libraries.Foundation -getDependencies -copyBinaries -downstream -skipUntil Libraries.Workflow
    will queue a build from the specified module and any modules which directly or indirectly depend on it but skip actually building any module
    until it reaches Libraries.Workflow. Useful for large builds that have failed somewhere in between and we want to pipck up from where we left off.
    The dependencies will be fetched before building and the output will be copied to the binaries folder.
#>
function Build-ExpertModules {
    param ([string[]]$workflowModuleNames, [switch] $changeset = $false, [switch] $clean = $false, [switch]$getDependencies = $false, [switch] $copyBinaries = $false, [switch] $downstream = $false, [switch] $getLatest = $false, [switch] $continue, [string[]] $getLocal, [string[]] $exclude, [string] $skipUntil, [switch]$debug, [switch]$release)

	if ($debug -and $release) {
		Write-Error "You can specify either -debug or -release but not both."
		return
	}

	if ($ShellContext.IsGitRepository) {
		Write-Error "You cannot run this command for a git repository. Use 'bm' or 'Invoke-Build' instead."
		return
	}

    $moduleBeforeBuild = $null

    try {
        $currentWorkingDirectory = Get-Location

        if (!$workflowModuleNames) {
          if (($global:CurrentModulePath) -and (Test-Path $global:CurrentModulePath)) {
             $moduleBeforeBuild = (New-Object System.IO.DirectoryInfo $global:CurrentModulePath | foreach {$_.Name})
             $workflowModuleNames = @($moduleBeforeBuild)
          }
        }

        $builtModules = @{}

        if (!$getLocal) {
            [string[]]$getLocal = @()
        }

        if (!$exclude) {
            [string[]]$exclude = @()
        }

        if ($continue) {
            if (!$global:LastBuildRemainingModules) {
                write "No previously failed build found"
                return
            }
            $builtModules = $global:LastBuildBuiltModules
            $workflowModuleNames = $global:LastBuildRemainingModules
            $getDependencies = $global:LastBuildGetDependencies
            $copyBinaries = $global:LastBuildCopyBinaries
            $downstream = $global:LastBuildDownstream
            $getLatest = $global:LastBuildGetLatest
            $getLocal = $global:LastBuildGetLocal
            $exclude = $global:LastBuildExclude
        }

        if ($changeset) {
             write ""
             write "Retrieving Expert modules for current changeset ..."
             [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$workflowModuleNames = $global:workspace.GetModulesWithPendingChanges($global:BranchModulesDirectory)
             write "Done."
        }

        # Set the new last build configuration
        $global:LastBuildGetDependencies = $getDependencies
        $global:LastBuildCopyBinaries = $copyBinaries
        $global:LastBuildDownstream = $downstream
        $global:LastBuildGetLatest = $getLatest
        $global:LastBuildRemainingModules = $workflowModuleNames
        $global:LastBuildGetLocal = $getLocal
        $global:LastBuildExclude = $exclude

        if (!($workflowModuleNames)) {
            write "No modules specified."
            return
        }

        [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$workflowModuleNames = $global:workspace.GetModules($workflowModuleNames)

        if ((Test-Path $BranchLocalDirectory) -ne $true) {
            write "Branch Root path does not exist: '$BranchLocalDirectory'"
        }

        [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$modules = Sort-ExpertModulesByBuildOrder -BranchPath $global:BranchModulesDirectory -Modules $workflowModuleNames -ProductManifestPath $global:ProductManifestPath

        if (!$modules -or (($modules.Length -ne $workflowModuleNames.Length) -and $workflowModuleNames.Length -gt 0)) {
            Write-Warning "After sorting builds by order the following modules were excluded."
            Write-Warning "These modules probably have no dependency manifest or do not exist in the Expert Manifest"

            (Compare-Object -ReferenceObject $workflowModuleNames -DifferenceObject $modules -Property Name -PassThru) | Select-Object -Property Name

            $message = "Do you want to continue anyway?"
            $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes"
            $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No"

            $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)
            $result = $host.UI.PromptForChoice($null, $message, $options, 0)

            if ($result -ne 0) {
                write "Module(s) not found."
                return
            }
        }

        if ($exclude -eq $null) {
            $exclude = @()
        }

        if ($downstream -eq $true) {
            write ""
            write "Retrieving downstream modules"

            [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$modules = $global:workspace.DependencyAnalyzer.GetDownstreamModules($modules)

            $modules = Sort-ExpertModulesByBuildOrder -BranchPath $global:BranchModulesDirectory -Modules $modules -ProductManifestPath $global:ProductManifestPath
            $modules = $modules | Where { $_.ModuleType -ne [Aderant.Build.DependencyAnalyzer.ModuleType]::Test }
            write "Done."
        }

        $modules = $modules | Where {$exclude -notcontains $_}

        write ""
        write "********** Build Overview *************"
        $count = 0
        $weHaveSkipped = $false

        foreach($module in $modules) {
          $count++
          $skipMarkup = ""
          if ($skipUntil -eq $module) {
              $weHaveSkipped = $true
          }
          if ($skipUntil -and $weHaveSkipped -ne $true){
              $skipMarkup = " (skipped)"
          }
          write "$count. $module $skipMarkup"
        }

        write ""
        write ""
        write "Press Ctrl+C to abort"
        Start-Sleep -m 2000

        $weHaveSkipped = $false

        foreach ($module in $modules) {
          if ($skipUntil -eq $module) {
            $weHaveSkipped = $true
          }

          # If the user specified skipUntil then we will skip over the modules in the list until we reach the specified one.
          if ($skipUntil -and $weHaveSkipped -eq $false) {
             Write-Host "************* $module *************"
             Write-Host "   Skipping  "
             # Add the module to the list of built modules
             if (!$builtModules.ContainsKey($module.Name)) {
               $builtModules.Add($module.Name, $module)
               $global:LastBuildBuiltModules = $builtModules
             }
          } else {
              # We either have not specified a skip or we have already skipped the modules we need to
              Set-CurrentModule $module.Name
              if ($getLatest){
                Get-LatestSourceForModule $module.Name -branchPath $BranchLocalDirectory
              }

              if ($getDependencies -eq $true){
                Get-DependenciesForCurrentModule
              }

              if ($builtModules -and $builtModules.Length -gt 0) {
                  $dependencies = Get-ExpertModuleDependencies -BranchPath $BranchLocalDirectory -SourceModule $module -IncludeThirdParty $true
                  Write-Host "************* $module *************"
                  foreach ($dependencyModule in $dependencies) {
                    Write-Debug "Module dependency: $dependencyModule"

                    if (($dependencyModule -and $dependencyModule.Name -and $builtModules.ContainsKey($dependencyModule.Name)) -or ($getLocal | Where {$_ -eq $dependencyModule})) {
                      $sourcePath = Join-Path $BranchLocalDirectory Modules\$dependencyModule\Bin\Module

                      if ($dependencyModule.ModuleType -eq [Aderant.Build.DependencyAnalyzer.ModuleType]::ThirdParty) {
                        # Probe the new style ThirdParty path
                        $root = [System.IO.Path]::Combine($BranchLocalDirectory, "Modules", "ThirdParty")
                        if ([System.IO.Directory]::Exists($root)) {
                            $sourcePath = [System.IO.Path]::Combine($root, $dependencyModule, "Bin")
                        } else {
                            # Fall back to the old style path
                            $root = [System.IO.Path]::Combine($BranchLocalDirectory, "Modules")
                            $sourcePath = [System.IO.Path]::Combine($root, $dependencyModule, "Bin")
                        }
                      }

                      if (-not [System.IO.Directory]::Exists($sourcePath)) {
                        throw "The path $sourcePath does not exist"
                      }

                      Write-Debug "Local dependency source path: $sourcePath"

                      $targetPath = Join-Path $BranchLocalDirectory Modules\$module
                      CopyContents $sourcePath "$targetPath\Dependencies"
                    }
                  }
              }

              # Do the Build
              if ($module.ModuleType -ne [Aderant.Build.DependencyAnalyzer.ModuleType]::ThirdParty) {
                  Start-BuildForCurrentModule $clean $debug

                  pushd $currentWorkingDirectory

                  # Check for errors
                  if ($LASTEXITCODE -eq 1) {
                      throw "Build of $module Failed"
                  }
              }

              # Add the module to the list of built modules
              if (!$builtModules.ContainsKey($module.Name)){
                  $builtModules.Add($module.Name, $module)
                  $global:LastBuildBuiltModules = $builtModules
              }

              # Copy binaries to drop folder
              if ($copyBinaries -eq $true){
                  Copy-BinariesFromCurrentModule
              }
            }

          [string[]]$global:LastBuildRemainingModules = $global:LastBuildRemainingModules | Where  {$_ -ne $module}
        }

        $global:LastBuildRemainingModules = $null

        if ($moduleBeforeBuild) {
          cm $moduleBeforeBuild
        }
      } finally {
        pushd $currentWorkingDirectory
        [Console]::TreatControlCAsInput = $false
      }
}

<#
.Synopsis
    Builds the current module on server.
.Description
    Builds the current module on server.
.Example
     bm -getDependencies -clean ; Build-ExpertModulesOnServer -downstream
    If a local build succeeded, a server build will then be kicked off for current module.
#>
function Build-ExpertModulesOnServer([string[]] $workflowModuleNames, [switch] $downstream = $false) {
    $moduleBeforeBuild = $null;
    $currentWorkingDirectory = Get-Location;

    if (!$workflowModuleNames) {
        if (($global:CurrentModulePath) -and (Test-Path $global:CurrentModulePath)) {
            $moduleBeforeBuild = (New-Object System.IO.DirectoryInfo $global:CurrentModulePath | foreach {$_.Name});
            $workflowModuleNames = @($moduleBeforeBuild);
        }
    }

    if (!($workflowModuleNames)) {
        write "No modules specified.";
        return;
    }

    [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$workflowModuleNames = $global:workspace.GetModules($workflowModuleNames)

    if ((Test-Path $BranchLocalDirectory) -ne $true) {
        write "Branch Root path does not exist: '$BranchLocalDirectory'"
    }

    [Aderant.Build.DependencyAnalyzer.ExpertModule[]] $modules = Sort-ExpertModulesByBuildOrder -BranchPath $global:BranchModulesDirectory -Modules $workflowModuleNames -ProductManifestPath $global:ProductManifestPath

    if (!$modules -or (($modules.Length -ne $workflowModuleNames.Length) -and $workflowModuleNames.Length -gt 0)) {
        Write-Warning "After sorting builds by order the following modules were excluded.";
        Write-Warning "These modules probably have no dependency manifest or do not exist in the Expert Manifest"

        (Compare-Object -ReferenceObject $workflowModuleNames -DifferenceObject $modules -Property Name -PassThru) | Select-Object -Property Name

        $message = "Do you want to continue anyway?";
        $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes"
        $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No"

        $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)
        $result = $host.UI.PromptForChoice($null, $message, $options, 0)

        if ($result -ne 0) {
            write "Module(s) not found."
            return
        }
    }

    if ($downstream -eq $true) {
        write ""
        write "Retrieving downstream modules"

        [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$modules = $global:workspace.DependencyAnalyzer.GetDownstreamModules($modules)

        $modules = Sort-ExpertModulesByBuildOrder -BranchPath $global:BranchModulesDirectory -Modules $modules -ProductManifestPath $global:ProductManifestPath
        $modules = $modules | Where { $_.ModuleType -ne [Aderant.Build.DependencyAnalyzer.ModuleType]::Test }
        write "Done."
    }

    $modules = $modules | Where {$exclude -notcontains $_}

    write ""
    write "********** Build Overview *************"
    $count = 0
    $weHaveSkipped = $false
    foreach($module in $modules) {
        $count++;
        write "$count. $module";
    }
    write "";
    write "";
    write "Press Ctrl+C now to abort.";
    Start-Sleep -m 2000;

    foreach($module in $modules) {
        $sourcePath = $BranchName.replace('\','.') + "." + $module;

        Write-Warning "Build(s) started attempting on server for $module, if you do not wish to watch this build log you can now press 'CTRL+C' to exit.";
        Write-Warning "Exiting will not cancel your current build on server. But it will cancel the subsequent builds if you have multiple modules specified, i.e. -downstream build.";
        Write-Warning "...";
        Write-Warning "You can use 'popd' to get back to your previous directory.";
        Write-Warning "You can use 'New-ExpertBuildDefinition' to create a new build definition for current module if non-exist."

        Invoke-Expression "tfsbuild start http://tfs:8080/tfs ExpertSuite $sourcePath";
    }
    if ($moduleBeforeBuild) {
        cm $moduleBeforeBuild;
    }
    pushd $currentWorkingDirectory;
}

<#
.Synopsis
    Forced stopping a visual studio solution for specific module(s).
.Description
    Forced stopping a visual studio solution for specific module(s).
.Example
    Kill-VisualStudio -killAll, Kill-VisualStudio Web.Case
    Forced shutting down all opened visual studio solutions; forced shutting down visual studio solution of Web.Case.
#>
function Kill-VisualStudio([string[]] $workflowModuleNames, [switch] $killAll = $false) {
    $moduleBeforeBuild = $null;
    $currentWorkingDirectory = Get-Location;

    if (!$workflowModuleNames) {
        if (($global:CurrentModulePath) -and (Test-Path $global:CurrentModulePath)) {
            $moduleBeforeBuild = (New-Object System.IO.DirectoryInfo $global:CurrentModulePath | foreach {$_.Name});
            $workflowModuleNames = @($moduleBeforeBuild);
        }
    }

    if($killAll) {
        Stop-Process -processname devenv;
    } else {
        $branchNameEscaped = $BranchName.replace('\','\\');
        if (!($workflowModuleNames)) {
            write "No modules specified.";
            return;
        }
        [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$workflowModuleNames = $global:workspace.GetModules($workflowModuleNames);
        $workflowModuleNames = $workflowModuleNames | Where {$exclude -notcontains $_};

        foreach($module in $workflowModuleNames) {
            $filter = "ExecutablePath LIKE '%devenv%' AND CommandLine LIKE '%$module%' AND CommandLine LIKE '%$branchNameEscaped%'";
            if (@(Get-WmiObject Win32_Process -Filter "$filter").count -eq 1) {
                Get-WmiObject Win32_Process -Filter "$filter" | Invoke-WmiMethod -Name Terminate;
                Write-Warning "Succeeded. Visual Studio Solution for $module in $BranchName has been forced shutting down.";
            } else {
                if (@(Get-WmiObject Win32_Process -Filter "$filter").count -eq 0) {
                    Write-Warning "Failed. An error occurred, command found none Visual Studio Solution for $module in $BranchName.";
                } else {
                    Write-Warning "Failed. An error occurred, command found more than 1 Visual Studio Solutions matches the criteria, please use -killAll option if you wish to kill all.";
                }
            }
        }
    }

    if ($moduleBeforeBuild) {
        cm $moduleBeforeBuild;
    }
    pushd $currentWorkingDirectory;
}

<#
.Synopsis
    Builds a patch for the current branch.
.Description
    Builds a patch for the current branch. Driven from the PatchingManifest.xml.
.Example
        Get-ProductZip; Build-ExpertPatch
    Gets the latest product zip from the build server then builds a patch using those binaries.
.Example
        Get-Product; Build-ExpertPatch
    Gets the latest product from the build server then builds a patch using those binaries.
.Example
        Build-ExpertPatch
    Builds the patch using the local binaries.
#>
function Build-ExpertPatch([switch]$noget = $false) {
    if (!$noget) {
        $cmd = "xcopy \\na.aderant.com\expertsuite\Main\Build.Tools\Current\* /S /Y $PackageScriptsDirectory"
        Invoke-Expression $cmd
    }
    pushd $PackageScriptsDirectory; .\Patching\BuildPatch.ps1
    popd
}


function Get-ExpertModulesInChangeset {
   return $global:workspace.GetModulesWithPendingChanges($global:BranchModulesDirectory)
}

function Get-LatestSourceForModule {
 param([string[]] $moduleNames, [string] $branchPath)

 foreach($moduleName in $moduleNames){
    write "*** Getting latest for $moduleName ****"
    $path = "$branchPath\Modules\$moduleName"
    Invoke-Expression "tf get $path /recursive"
 }
}

<#
.Synopsis
    Starts DeploymentManager for your current branch
.Description
    DeploymentManager
#>
function Start-DeploymentManager {
    $shell = ".\DeploymentManager.exe $fullManifest"
    switch ($global:BranchExpertVersion) {
        "8" {
            #8.0 case where ExperSource and Deployment folders exist in binaries folder, and DeploymentManager is renamed to Setup.exe.
            $shell = ".\Setup.exe $fullManifest"
            pushd $global:BranchBinariesDirectory
            Write "8.x Starting Deployment Manager (Setup.exe) in Binaries folder..."
        }
        "802" {
            #8.0.2 case where ExperSource folder exists in binaries folder, and DeploymentManager is renamed to Setup.exe.
            $shell = ".\Setup.exe $fullManifest"
            pushd $global:BranchBinariesDirectory
            Write "8.0.2 Starting Deployment Manager (Setup.exe) in Binaries folder..."
        }
        "8.1.0" {
            if (Test-Path $ShellContext.DeploymentManager) {
                $shell = $ShellContext.DeploymentManager                
            } else {
                $shell = $null
                InstallDeployment                                
            }
        }
        default {

        }
    }

    if ($shell) {
        Invoke-Expression $shell
    }
    popd
}

<#
.Synopsis
    Run DeploymentEngine for your current branch
.Description
    DeploymentEngine -action deploy -serverName MyServer01 -databaseName MyMain
    DeploymentEngine -action remove -serverName MyServer01 -databaseName MyMain
    Parameter $action, the action you want the deployment engine to take
    Parameter serverName name of the database server
    Parameter databaseName name of the database containing the environment manifest
    Parameter skipPackageImports flag to skip package imports
    Paramater skipHelpDeployment flag to skip deployment of Help
#>
function Start-DeploymentEngine {
    param ([string]$action, [string]$serverName, [string]$databaseName, [switch]$skipPackageImports = $false, [switch]$skipHelpDeployment = $false)

    if (Test-Path $ShellContext.DeploymentEngine) {

        $environmentXmlPath = [System.IO.Path]::Combine($global:BranchBinariesDirectory, "environment.xml")
        if (Test-Path $environmentXmlPath) {
            [xml]$env = Get-Content -Path $environmentXmlPath

            $serverName = $env.environment.expertDatabaseServer.serverName
            $databaseName = $env.environment.expertDatabaseServer.databaseConnection.databaseName
        }

        $shell = "$($ShellContext.DeploymentEngine) $action /s:$serverName /d:$databaseName"
        if ($skipPackageImports) {
            $shell = "$shell /skp"
        }
        if ($skipHelpDeployment) {
            $shell = "$shell /skh"
        }
        Write-Host "Executing $shell"
        Invoke-Expression $shell
    } else {
        InstallDeployment
    }
}

function InstallDeployment() {
    Write-Host "Deployment is not installed to the default path."

    $title = "Install Deployment"
    $message = "Do you want to install Deployment?"

    $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes", "Really?"
    $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No", "No way!"

    $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)

    $result = $host.ui.PromptForChoice($title, $message, $options, 0)

    if ($result -eq 0) {
        [System.IO.FileInfo]$msi = gci $global:BranchBinariesDirectory -Filter "DeploymentManager.msi" -Recurse -File | Select-Object -First 1
        & $msi.FullName
    }
}

#sets up visual studio environment, called from Profile.ps1 when starting PS.
function Set-Environment($initialize = $true) {
    if ($initialize) {
        Set-BranchPaths
    }

    Set-ScriptPaths
    Set-ExpertSourcePath
    Initialise-BuildLibraries
    Set-VisualStudioVersion

    OutputEnvironmentDetails

    $global:workspace = new-object -TypeName Aderant.Build.Providers.ModuleWorkspace -ArgumentList $Global:ProductManifestPath,$global:BranchModulesDirectory,"ExpertSuite"

    if ($initialize) {
        # Setup PowerShell script unit test environment
        InstallPester
    }
}

<#
 Re-set the local working branch
 e.g. Dev\Product or MAIN
#>
function SwitchBranchTo($newBranch, [switch] $SetAsDefault) {
    if ($global:BranchName -Contains $newBranch) {
        Write-Host -ForegroundColor Yellow "The magic unicorn has refused your request."
        return
    }

    $success = Set-ChangedBranchPaths $newBranch

    if ($success -eq $false) {
        return
    }

    Set-Environment $false

    Set-CurrentModule $global:CurrentModuleName

    cd $global:BranchLocalDirectory

    if ($SetAsDefault) {
        SetDefaultValue dropRootUNCPath $BranchServerDirectory
        SetDefaultValue devBranchFolder $BranchLocalDirectory
    }
}

function Set-VisualStudioVersion() {
    $file = [System.IO.Path]::Combine($global:BuildScriptsDirectory, "vsvars.ps1");
    if (Test-Path $file) {
        &($file)
    }
}


# gets a value from the global defaults storage, or creates a default
function global:GetDefaultValue {
    param (
        [string]$propertyName,
        [string]$defaultValue
    )

    Write-Debug "Asked for default for: $propertyName with default ($defaultValue)"

    if ([Environment]::GetEnvironmentVariable("Expert$propertyName", "User") -ne $null) {
        return [Environment]::GetEnvironmentVariable("Expert$propertyName", "User")
    }

    if ($propertyName -eq "DevBranchFolder") {
        cls
        return SetPathVariable "Where is your local path to the MAIN branch? e.g C:\tfs\ExpertSuite\Main" $propertyName
    }

    if ($propertyName -eq "DropRootUNCPath") {
        return SetPathVariable "Where is the MAIN branch drop path? For e.g \\dfs.aderant.com\ExpertSuite\Main" $propertyName
    }

    if ($propertyName -eq "SystemMapConnectionString") {
        $title = "SystemMapBuilder Setup"
        $message = "Do you want to use the Expert ITE for SystemMap generation?"
        $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes"
        $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No"

        $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)
        $result = $host.UI.PromptForChoice($title, $message, $options, 0)

        if ($result -eq 0) {
            $connection = "/pdbs:svsql303\mssql10 /pdbd:VMAKLITEGG /pdbid:cmsdbo /pdbpw:cmsdbo"
            SetDefaultValue $propertyName $connection
            return $connection
        } else {
            $server = Read-Host "What is the database server?"
            $database = Read-Host "What is the database to use?"
            $login = Read-Host "What is the database login?"
            $password = Read-Host "What is the login password?"

            $connection = "/pdbs:$server /pdbd:$database /pdbid:$login /pdbpw:$password"
            SetDefaultValue $propertyName $connection
            return $connection
        }
    }

    # Environment variable was not set, default it.
    [Environment]::SetEnvironmentVariable("Expert$propertyName", $defaultValue, "User")
    return $defaultValue
}

function SetPathVariable($question, $propertyName) {
    $result = $null
    while ([string]::IsNullOrEmpty($result)) {
        $result = Read-Host $question

        if (-not ([string]::IsNullOrEmpty($result))) {
           if (Test-Path $result) {
                SetDefaultValue $propertyName $result
                return $result
            } else {
                Write-Warning "The given path $result does not exist"
                $result = $null
            }
        }
    }
}

<#
.Synopsis
    Sets the default branch information.
.Description
    Sets the default branch information. This was formerly held in the defaults.xml file. After initially setting this information
    you should use the Switch-Branch command with the -SetAsDefault parameter to update it.
.PARAMETER devBranchFolder
    The full path to the development branch
.PARAMETER dropUncPath
    The full unc path to the network drop folder for the branch
.EXAMPLE
        Set-ExpertBranchInfo -devBranchFolder c:\ExpertSuite\Dev\Msg2 -dropUncPath C:\expertsuite\Dev\Msg2
     Will set the default branch information to the Dev\Msg2 branch

#>
function Set-ExpertBranchInfo([string] $devBranchFolder, [string] $dropUncPath) {
    if((Test-Path $devBranchFolder) -ne $true){
        Write-Error "The path $devBranchFolder does not exist"
    }

    if((Test-Path $dropUncPath) -ne $true){
        Write-Error "The path $dropUncPath does not exist"
    }

    SetDefaultValue DevBranchFolder $devBranchFolder
    SetDefaultValue DropRootUNCPath $dropUncPath
    Set-Environment $true
    Write-Host ""
    Write-Host "The environment has been configured"
    Write-Host "You should not have to run this command again on this machine"
    Write-Host "In future when changing branches you should use the Switch-Branch command with the -SetAsDefault parameter to make it permanent."
}

# sets a value in the global defaults storage
function global:SetDefaultValue {
    param (
        [string]$propertyName,
        [string]$defaultValue
    )

    [Environment]::SetEnvironmentVariable("Expert$propertyName", $defaultValue, "User")

}

<#
.Synopsis
    Opens the solution for a module in the current branch
.Description
    Opens a module's main solution in visual studio
.PARAMETER ModuleName
    The name of the module
.PARAMETER getDependencies
    This will get the latest dependencies for the selected module before opening it up in visual studio.
.PARAMETER getLatest
    This will get the latest source code for the selected module before opening it up in visual studio.
.EXAMPLE
        Open-ModuleSolution Libraries.Presentation
    Will open the Libraries.Presentation solution in visual studio

#>
function Open-ModuleSolution([string] $ModuleName, [switch] $getDependencies, [switch]$getLatest) {
    if (($getDependencies) -and -not [string]::IsNullOrEmpty($ModuleName)) {
        if (-not [string]::IsNullOrEmpty($global:CurrentModuleName)) {
            $prevModule = Get-CurrentModule
        }
        Set-CurrentModule $moduleName
    }
	$rootPath = Join-Path $BranchLocalDirectory "Modules\$ModuleName"
    if(!($ModuleName)){
        $ModuleName = $global:CurrentModuleName
		$rootPath = $global:CurrentModulePath
    }
    if(!($ModuleName) -or $ModuleName -eq $null -or $ModuleName -eq ""){
        Write-Host -ForegroundColor Yellow "No module specified"
        return
    }
    if ($getDependencies) {
        Write-Host "Getting Dependencies for module $ModuleName"
        Get-DependenciesForCurrentModule
    }
    if ($getLatest) {
        Write-Host "Getting latest source for module $ModuleName"
        Get-Latest -ModuleName $ModuleName;
    }
    Write-Host "Opening solution for module $ModuleName"
    $moduleSolutionPath = Join-Path $rootPath "$ModuleName.sln"
    if(Test-Path $moduleSolutionPath) {
        Invoke-Expression "devenv $moduleSolutionPath"
    } else {
		$candidates = (gci -Filter *.sln -file  | Where-Object {$_.Name -NotMatch ".custom.sln"})
		if ($candidates.Count -gt 0) {
			$moduleSolutionPath = Join-Path $rootPath $candidates[0]
			Invoke-Expression "devenv $moduleSolutionPath"
		} else {
			"There is no solution file at $moduleSolutionPath"
		}
    }
    if (($prevModule) -and (Get-CurrentModule -ne $prevModule)) {
        Set-CurrentModule $prevModule
    }
}
<#
.Synopsis
    Gets the latest source from TFS
.Description
    Get the latest source from TFS for a module, or if no module is specified, the current module, or if -Branch is specified, the entire branch
.PARAMETER ModuleName
    The name of the module
.PARAMETER Branch
    Gets latest for the entire branch instead of a particular module
.EXAMPLE
        Get-Latest Libraries.Presentation
    Gets the latest source for Libraries.Presentation from TFS
.EXAMPLE
        Get-Latest
    Gets the latest source for the current module from TFS
.EXAMPLE
        Get-Latest -Branch
    Gets the latest source for the current branch from TFS
#>
function Get-Latest([string] $ModuleName, [switch] $Branch)
{
    $sourcePath = $null;
    if($Branch){
        $sourcePath = $global:BranchLocalDirectory
    }
    else {
        if(!($ModuleName)){
            $ModuleName = $global:CurrentModuleName
        }
        if(!($ModuleName) -or $ModuleName -eq $null -or $ModuleName -eq ""){
            "No module specified"
            return
        }
        $sourcePath = Join-Path $global:BranchLocalDirectory "Modules\$ModuleName"
        if((Test-Path $sourcePath) -eq $False) {
            "There is no local path $sourcePath. Make sure you are specifying a module that exists in the current branch"
            return
        }
    }

    Invoke-Expression "tf get $sourcePath /recursive"
}

# This will start the UI Test Runner for provisioning automated test environments and starting the tests.
function Start-UITests([switch]$noUpdate, [string]$TestRunnerPath) {
    if ([string]::IsNullOrWhiteSpace($TestRunnerPath)) {
        $TestRunnerPath = "C:\temp\UIAutomation"
    }
    if (-not $noUpdate) {
    $UITestRunnerPath = "\\na.aderant.com\packages\Infrastructure\Automation\UIAutomation\UIAutomation.TestRunner\5.3.1.0"
    $shell = Join-Path $BuildScriptsDirectory Build-Libraries.ps1
    $shell = "& { . $shell; PathToLatestSuccessfulBuild $UITestRunnerPath }"
    $latestSuccessfulBuild = powershell -noprofile -command $shell
        if ( -not (Test-Path $TestRunnerPath)) {
            New-Item $TestRunnerPath -type directory
        }
        Invoke-Expression "robocopy.exe /S /PURGE /NJH /NJS /NS /NC /NP /NFL /NDL /R:5 /W:1 /MT:3 $latestSuccessfulBuild $TestRunnerPath"
    }
    $testTool = Join-Path $TestRunnerPath "UITester.exe"
    Invoke-Expression "$testTool -BranchModulesDirectory $BranchModulesDirectory -BuildScriptsDirectory $BuildScriptsDirectory -PackageScriptsDirectory $PackageScriptsDirectory -environmentmanifestpath C:\ExpertShare\environment.xml -branchserverdirectory $BranchServerDirectory"
}



function TabExpansion([string] $line, [string] $lastword)
{
    if (!$lastword.Contains(";")) {
        $aliases = Get-Alias
        $parser = New-Object Aderant.Build.AutoCompletionParser $line, $lastword, $aliases

        # Evaluate Branches
        Try {
            foreach($tabExpansionParm in $global:expertTabBranchExpansions){
                if ($parser.IsAutoCompletionForParameter($tabExpansionParm.CommandName.ToString(), $tabExpansionParm.ParameterName.ToString(), $tabExpansionParm.IsDefault.IsPresent)) {
                    Get-ExpertBranches $lastword | Get-Unique
                }
            }
        }
        Catch
        {
            [system.exception]
            Write-Host $_.Exception.ToString()
        }        
    }

    [System.Diagnostics.Debug]::WriteLine("Aderant Build Tools:Falling back to default tab expansion for Last word: $lastword, Line: $line")   
}

function IsTabOnCommandParameter([string] $line, [string] $lastword, [string] $commandName, [string] $parameterName, [switch] $isDefaultParameter) {
    # Need to select last command if is a line of commands separated by ";"
    # Need to ignore auto-completion of parameters
    # Need to ignore auto-completion of command names
    #return ($lastword.StartsWith("-") -ne True -and $line -ne $commandName -and (($line.Trim() -eq "$commandName $lastword" -and $isDefaultParameter) -or ($line.Trim() -eq "SwitchBranchTo")))
}

Export-ModuleMember -Function TabExpansion

$global:expertTabBranchExpansions = @()

<#
.Synopsis
    Adds a parameter to the expert tab expansions for modules
.Description
    Tab Expansion is when pressing tab will auto-complete the value of a parameter. This command allows you to configure autocomplete where a module name or comma separated list of module names is required
.PARAMETER CommandName
    The name of the command (not the alias)
.PARAMETER ParameterName
    The name of the parameter to match
.EXAMPLE
    Add-ModuleExpansionParameter -CommandName Build-ExpertModules -ParameterName ModuleNames -IsDefault
    Will add tab expansion of module names on the Build-ExpertModules command where the current parameter is the ModuleNames parameter
#>
function Add-ModuleExpansionParameter([string] $CommandName, [string] $ParameterName){
    if (!($CommandName)) {
        write "No command name specified."
        return
    }
    if (!($CommandName)) {
        write "No parameter name specified."
        return
    }    
       
    Register-ArgumentCompleter -CommandName $CommandName -ParameterName $ParameterName -ScriptBlock {
        param($commandName, $parameterName, $wordToComplete, $commandAst, $boundParameters)

        $aliases = Get-Alias
        $parser = New-Object Aderant.Build.AutoCompletionParser $commandName, $parameterName, $commandAst
                
        # Evaluate Modules
        Try {   
            $parser.GetModuleMatches($wordToComplete, $global:BranchModulesDirectory, $ProductManifestPath) | Get-Unique | ForEach-Object {                    
                [System.Management.Automation.CompletionResult]::new($_)
            } 
            
            # Probe for known Git repositories
            gci -Path "HKCU:\SOFTWARE\Microsoft\VisualStudio\14.0\TeamFoundation\GitSourceControl\Repositories" | % { Get-ItemProperty $_.pspath } |           
                Where-Object { $_.Name -like "$wordToComplete*" } | ForEach-Object {                    
                    [System.Management.Automation.CompletionResult]::new($_.Path)
                }           
        }
        Catch
        {
            [system.exception]           
            Write-Host $_.Exception.ToString()
        }
    }
}



<#
.Synopsis
    Adds a parameter to the expert tab expansions for branches
.Description
    Tab Expansion is when pressing tab will auto-complete the value of a parameter. This command allows you to configure autocomplete where a branch name is required
.PARAMETER CommandName
    The name of the command (not the alias)
.PARAMETER ParameterName
    The name of the parameter to match
.PARAMETER IsDefault
    Set if this is the default index 0 parameter
.EXAMPLE
    Add-BranchExpansionParameter -CommandName SwitchBranchTo -ParameterName newBranch -IsDefault

    Will add tab expansion of branch names on the newBranch command where the current parameter is the newBranch parameter and this is also the first (default) parameter

#>
function Add-BranchExpansionParameter([string] $CommandName, [string] $ParameterName, [switch] $IsDefault){
    if (!($CommandName)) {
        write "No command name specified."
        return
    }
    if (!($CommandName)) {
        write "No parameter name specified."
        return
    }
    $objNewExpansion = New-Object System.Object
    $objNewExpansion | Add-Member -type NoteProperty -name CommandName -value $CommandName
    $objNewExpansion | Add-Member -type NoteProperty -name ParameterName -value $ParameterName
    $objNewExpansion | Add-Member -type NoteProperty -name IsDefault -value $IsDefault
    $global:expertTabBranchExpansions += $objNewExpansion
}



# Add branch auto completion scenarios
Add-BranchExpansionParameter -CommandName "SwitchBranchTo" -ParameterName "newBranch" -IsDefault
Add-BranchExpansionParameter -CommandName "Branch-Module" -ParameterName "sourceBranch"
Add-BranchExpansionParameter -CommandName "Branch-Module" -ParameterName "targetBranch"

Add-BranchExpansionParameter –CommandName "New-ExpertManifestForBranch" –ParameterName "SourceBranch" -IsDefault
Add-BranchExpansionParameter –CommandName "New-ExpertManifestForBranch" –ParameterName "TargetBranch"
Add-BranchExpansionParameter -CommandName "Move-Shelveset" -ParameterName "TargetBranch"
Add-BranchExpansionParameter -CommandName "Move-Shelveset" -ParameterName "SourceBranch"

#These commands are in AderantTfs.psm1
Add-BranchExpansionParameter -CommandName "Merge-Branch" -ParameterName "sourceBranch"
Add-BranchExpansionParameter -CommandName "Merge-Branch" -ParameterName "targetBranch"
Add-BranchExpansionParameter -CommandName "Merge-Baseless" -ParameterName "sourceBranch"
Add-BranchExpansionParameter -CommandName "Merge-Baseless" -ParameterName "targetBranch"

# Add module auto completion scenarios
Add-ModuleExpansionParameter -CommandName "Set-CurrentModule" -ParameterName "name"
Add-ModuleExpansionParameter -CommandName "Branch-Module" -ParameterName "moduleName"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "workflowModuleNames"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "getLocal"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "exclude"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "skipUntil"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModulesOnServer" -ParameterName "workflowModuleNames"
Add-ModuleExpansionParameter -CommandName "Kill-VisualStudio" -ParameterName "workflowModuleNames"
Add-ModuleExpansionParameter -CommandName "Get-DependenciesForCurrentModule" -ParameterName "onlyUpdated"
Add-ModuleExpansionParameter -CommandName "Get-Product" -ParameterName "onlyUpdated"
Add-ModuleExpansionParameter -CommandName "Get-DependenciesFrom" -ParameterName "ProviderModules"
Add-ModuleExpansionParameter -CommandName "Get-DependenciesFrom" -ParameterName "ConsumerModules"
Add-ModuleExpansionParameter -CommandName "Get-ExpertModuleDependencies" -ParameterName "SourceModuleName"
Add-ModuleExpansionParameter -CommandName "Get-ExpertModuleDependsOn" -ParameterName "TargetModuleName"
Add-ModuleExpansionParameter -CommandName "Get-DownstreamExpertModules" -ParameterName "ModuleName"
Add-ModuleExpansionParameter -CommandName "Get-ExpertModule" -ParameterName "ModuleName"
Add-ModuleExpansionParameter -CommandName "Get-ExpertModules" -ParameterName "ModuleNames"
Add-ModuleExpansionParameter –CommandName "Open-ModuleSolution" –ParameterName "ModuleName"
Add-ModuleExpansionParameter –CommandName "Get-Latest" –ParameterName "ModuleName"
Add-ModuleExpansionParameter –CommandName "Start-Redeployment" –ParameterName "CopyBinariesFrom"
Add-ModuleExpansionParameter -CommandName "Copy-BinToEnvironment" -ParameterName "ModuleNames"
Add-ModuleExpansionParameter -CommandName "Open-Directory" -ParameterName "ModuleName"
Add-ModuleExpansionParameter -CommandName "New-ExpertBuildDefinition" -ParameterName "ModuleName"

Add-ModuleExpansionParameter -CommandName "Clean" -ParameterName "moduleNames"
Add-ModuleExpansionParameter -CommandName "Scorch" -ParameterName "moduleNames"

Add-ModuleExpansionParameter –CommandName "Get-WebDependencies" –ParameterName "ModuleName"

<#
.Synopsis
    Enables the Expert prompt with branch and module information
.Description
    Enable-ExpertPrompt
#>
function Enable-ExpertPrompt() {

    Function global:prompt
    {
        # set the window title to the branch name
        $Host.UI.RawUI.WindowTitle = "PS - [" + $global:CurrentModuleName + "] on branch [" + $global:BranchName + "]"

        Write-Host("")
        Write-Host ("Module [") -nonewline
        Write-Host ($global:CurrentModuleName) -nonewline -foregroundcolor DarkCyan
        Write-Host ("] on branch [") -nonewline
        Write-Host ($global:BranchName) -nonewline -foregroundcolor Green
        Write-Host ("]")

        Write-Host ("PS " + $(get-location) +">") -nonewline
        return " "
    }
}

<#
.Synopsis
    Disables the Expert prompt with branch and module information
.Description
    Disable-ExpertPrompt
#>
function Disable-ExpertPrompt() {
    # Copy the current prompt function so we can fall back to it if we're not supposed to handle a command
            Function global:prompt
            {
                  $(if (test-path variable:/PSDebugContext) { '[DBG]: ' }
                  else { '' }) + 'PS ' + $(Get-Location) `
                  + $(if ($nestedpromptlevel -ge 1) { '>>' }) + '> '
            }

}

<#
.Synopsis
    Generates a System Map to the expertsource folder of the current branch
.Description
    Generates a System Map using the the default ITE information to the expertsource folder of the current branch
#>
function Generate-SystemMap(){
    $inDirectory = Join-Path $global:BranchBinariesDirectory 'ExpertSource'
    $systemMapConnectionString = (Get-SystemMapConnectionString)
    if($systemMapConnectionString.ToLower().Contains("/pdbs:") -and $systemMapConnectionString.ToLower().Contains("/pdbd:") -and $systemMapConnectionString.ToLower().Contains("/pdbid:") -and $systemMapConnectionString.ToLower().Contains("/pdbpw:")){
        $connectionParts = $systemMapConnectionString.Split(" ")
        Write-Debug "Connection is [$connectionParts]"
        &$inDirectory\Systemmapbuilder.exe /f:$inDirectory /o:$inDirectory\systemmap.xml /ef:Customization $connectionParts[0] $connectionParts[1] $connectionParts[2] $connectionParts[3]
    }else{
        Write-Error "Connection string is invalid for use with systemmapbuilder.exe [$systemMapConnectionString]"
    }
}

function Test-ReparsePoint([string]$path) {
  $file = Get-Item $path -Force -ea 0
  return [bool]($file.Attributes -band [IO.FileAttributes]::ReparsePoint)
}


<#
.Synopsis
    Determines the location of the Aderant Module.
.Description
    Determines the location of the Aderant Module.
#>
function Get-AderantModuleLocation() {
    $aderantModuleBase = (Get-Module -Name Aderant).ModuleBase
    if (Test-ReparsePoint $aderantModuleBase ){
        # this is a symlink, get the target.
        return Get-SymbolicLinkTarget $aderantModuleBase
    } else {
        # this is a normal folder.
        return $aderantModuleBase
    }
}

<#
.Synopsis
    cd to the specified directory.
.Description
    Will change your working directory to the specified directory. NOTE: this is similar to odir.
.PARAMETER BuildScripts
    cd to your powershell directory. (often a symlink to BuildScriptsForBranch)
.PARAMETER BuildScriptsForBranch
    cd to your build scripts in Build.Infrastructure.
.PARAMETER Binaries
    cd to your branch binaries directory.
.PARAMETER ExpertSource
    cd to your branch ExpertSource, often in your binaries directory.
.PARAMETER LocalBranch
    cd to your currently selected branch on your local disk.
.PARAMETER ServerBranch
    cd to your currently selected branch on the drop server.
.PARAMETER AllModules
    cd to the Modules directory for your currently selected branch.
.PARAMETER Module
    cd to the currently selected module's directory.
.PARAMETER ModuleBin
    cd to the bin directory for your currently selected module.
.PARAMETER ModuleDependencies
    cd to the dependency directory for your currently selected module.
.PARAMETER ExpertShare
    cd to the ExpertShare for your currently selected branch.
.PARAMETER ExpertLocal
    cd to your expert local directory, normally where the binaries for your services are stored.
.PARAMETER SharedBin
    cd to your sharedbin directory with the shared binaries for your services.
.EXAMPLE
        Change-Directory -ModuleBin
    Will cd to your currently selected module's bin directory.
#>
function Change-Directory(
    [switch]$BuildScripts, [switch]$BuildScriptsForBranch, [switch]$Binaries, [switch]$ExpertSource, [switch]$LocalBranch, [switch]$ServerBranch, [switch]$AllModules,
    [switch]$Module, [switch]$ModuleBin, [switch]$ModuleDependencies,
    [switch]$ExpertShare, [switch]$ExpertLocal, [switch]$SharedBin) {

    if (
        -not $BuildScripts -and
        -not $BuildScriptsForBranch -and
        -not $Binaries -and
        -not $ExpertSource -and
        -not $LocalBranch -and
        -not $ServerBranch -and
        -not $AllModules -and
        -not $Module -and
        -not $ModuleBin -and
        -not $ModuleDependencies -and
        -not $ExpertShare -and
        -not $ExpertLocal -and
        -not $SharedBin) {

        Write-Host -ForegroundColor Yellow "Please include at least one location.";
        Write-Host "-BuildScripts, -BuildScriptsForBranch";
        Write-Host "-Binaries, -ExpertSource, -ExpertShare, -ExpertLocal, -SharedBin";
        Write-Host "-AllModules, -Module, -ModuleBin, -ModuleDependencies";
        Write-Host "-LocalBranch, -ServerBranch";
    }

    if ($BuildScripts) {
        $path = [System.IO.Path]::Combine("C:\Users\", [Environment]::UserName);
        $path = [System.IO.Path]::Combine($path, "Documents\WindowsPowerShell");
        cd $path;
    }
    if ($BuildScriptsForBranch) {
        cd $global:BuildScriptsDirectory;
    }
    if ($Binaries) { #product bin
        cd "$global:BranchBinariesDirectory";
    }
    if ($ExpertSource) {
        cd "$global:BranchExpertSourceDirectory";
    }
    if ($LocalBranch) {
        cd "$global:BranchLocalDirectory";
    }
    if ($ServerBranch) {
        cd "$global:BranchServerDirectory";
    }
    if ($AllModules) {
        cd "$global:BranchModulesDirectory";
    }

    if ($Module) {
        if (Test-Path variable:global:CurrentModulePath) {
            cd "$global:CurrentModulePath";
        } else {
            Write-Host -ForegroundColor Yellow "Sorry you do not have a module selected."
        }
    }
    if ($ModuleBin) {
        if (Test-Path variable:global:CurrentModulePath) {
            $path = [System.IO.Path]::Combine($global:CurrentModulePath, "Bin");
            cd $path;
        } else {
            Write-Host -ForegroundColor Yellow "Sorry you do not have a module selected."
        }
    }
    if ($ModuleDependencies) {
        if (Test-Path variable:global:CurrentModulePath) {
            $path = [System.IO.Path]::Combine($global:CurrentModulePath, "Dependencies");
            cd $path;
        } else {
            Write-Host -ForegroundColor Yellow "Sorry you do not have a module selected."
        }
    }
    if ($ExpertShare) {
        # C:\ExpertShare
        $path = Get-EnvironmentFromXml "/environment/@networkSharePath";
        cd $path;
    }
    if ($ExpertLocal) {
        # C:\AderantExpert\Local
        $path = Get-EnvironmentFromXml "/environment/servers/server/@expertPath"
        cd $path;
    }
    if ($SharedBin) {
        # C:\AderantExpert\Local\SharedBin
        $expertLocalPath = Get-EnvironmentFromXml("/environment/servers/server/@expertPath");
        $path = [System.IO.Path]::Combine($expertLocalPath, "SharedBin");
        cd $path;
    }
    #TODO: TFS root.
}

<#
.Synopsis
    Open the specified directory in Exploiter.
.Description
    Will open all specified directories that exist in Explorer.exe
.PARAMETER BuildScripts
    Opens your powershell directory. (often a symlink to BuildScriptsForBranch)
.PARAMETER BuildScriptsForBranch
    Opens your build scripts in Build.Infrastructure.
.PARAMETER Binaries
    Opens your branch binaries directory.
.PARAMETER ExpertSource
    Opens your branch ExpertSource, often in your binaries directory.
.PARAMETER LocalBranch
    Opens your currently selected branch on your local disk.
.PARAMETER ServerBranch
    Opens your currently selected branch on the drop server.
.PARAMETER AllModules
    Opens the Modules directory for your currently selected branch.
.PARAMETER Module
    Opens the currently selected module's directory.
.PARAMETER ModuleBin
    Opens the bin directory for your currently selected module.
.PARAMETER ModuleDependencies
    Opens the dependency directory for your currently selected module.
.PARAMETER ExpertShare
    Opens the ExpertShare for your currently selected branch.
.PARAMETER ExpertLocal
    Opens your expert local directory, normally where the binaries for your services are stored.
.PARAMETER SharedBin
    Opens your sharedbin directory with the shared binaries for your services.
.EXAMPLE
        Open-Directory -ModuleBin -SharedBin
    Will open up both the binary directory of the selected module, and the sharedbin in ExpertLocal.
#>
function Open-Directory(
    [switch]$BuildScripts, [switch]$BuildScriptsForBranch, [switch]$Binaries, [switch]$ExpertSource, [switch]$LocalBranch, [switch]$ServerBranch, [switch]$AllModules,
    [switch]$Module, [switch]$ModuleBin, [switch]$ModuleDependencies,
    [switch]$ExpertShare, [switch]$ExpertLocal, [switch]$SharedBin,
    [string]$ModuleName) {

    #TODO: Could add a $paths which enables the user to specify arbritrary paths.

    if (
        -not $BuildScripts -and
        -not $BuildScriptsForBranch -and
        -not $Binaries -and
        -not $ExpertSource -and
        -not $LocalBranch -and
        -not $ServerBranch -and
        -not $AllModules -and
        -not $Module -and
        -not $ModuleBin -and
        -not $ModuleDependencies -and
        -not $ExpertShare -and
        -not $ExpertLocal -and
        -not $SharedBin) {

        Write-Host -ForegroundColor Yellow "Please include at least one location.";
        Write-Host "-BuildScripts, -BuildScriptsForBranch";
        Write-Host "-Binaries, -ExpertSource, -ExpertShare, -ExpertLocal, -SharedBin";
        Write-Host "-AllModules, -Module, -ModuleBin, -ModuleDependencies";
        Write-Host "-LocalBranch, -ServerBranch";
    }

    if ($BuildScripts) {
        $path = [System.IO.Path]::Combine("C:\Users\", [Environment]::UserName);
        $path = [System.IO.Path]::Combine($path, "Documents\WindowsPowerShell");
        Explorer($path);
    }
    if ($BuildScriptsForBranch) {
        Explorer($global:BuildScriptsDirectory);
    }
    if ($Binaries) { #product bin
        Explorer("$global:BranchBinariesDirectory");
    }
    if ($ExpertSource) {
        Explorer("$global:BranchExpertSourceDirectory");
    }
    if ($LocalBranch) {
        Explorer("$global:BranchLocalDirectory");
    }
    if ($ServerBranch) {
        Explorer("$global:BranchServerDirectory");
    }
    if ($AllModules) {
        Explorer("$global:BranchModulesDirectory");
    }
    if ($Module -or $ModuleBin -or $ModuleDependencies) {
        if (Test-Path variable:global:CurrentModulePath) {
            if ([string]::IsNullOrWhiteSpace($ModuleName)) {
                $selectedModulePath = $global:CurrentModulePath;
            } else {
                $firstHalf = $global:CurrentModulePath.Substring(0, $global:CurrentModulePath.LastIndexOf("\"));
                $selectedModulePath = [System.IO.Path]::Combine($firstHalf, $ModuleName);
            }
            if (Test-Path variable:selectedModulePath) {
                if ($Module) {
                    Explorer("$selectedModulePath");
                }
                if ($ModuleBin) {
                    $path = [System.IO.Path]::Combine($selectedModulePath, "Bin");
                    Explorer($path);
                }
                if ($ModuleDependencies) {
                    $path = [System.IO.Path]::Combine($selectedModulePath, "Dependencies");
                    Explorer($path);
                }
            } else {
                Write-Host -ForegroundColor Yellow "You seem to have misspelled the name of the module (or it doesn't exist in the current branch)."
            }
        } else {
            Write-Host -ForegroundColor Yellow "Sorry you do not have a module selected. Please select one first."
        }
    }
    if ($ExpertShare) {
        # C:\ExpertShare
        Explorer(Get-EnvironmentFromXml "/environment/@networkSharePath");

    }
    if ($ExpertLocal) {
        # C:\AderantExpert\Local
        Explorer(Get-EnvironmentFromXml "/environment/servers/server/@expertPath")
    }
    if ($SharedBin) {
        # C:\AderantExpert\Local\SharedBin
        $expertLocalPath = Get-EnvironmentFromXml("/environment/servers/server/@expertPath");
        $path = [System.IO.Path]::Combine($expertLocalPath, "SharedBin");
        Explorer($path);
    }
    #TODO: TFS
}
function Explorer([string]$path, [switch]$quiet) {
    if (Test-Path $path) {
        Invoke-Expression "explorer.exe $path";
        if (-not $quiet) {
            Write-Host "Opened: $path";
        }
    } else {
        Write-Host -ForegroundColor Red -NoNewline "  Directory does not exist for:";
        Write-Host " $path";
    }
}
function Get-EnvironmentFromXml([string]$xpath) {
    #I'd love it if this returned an object model representation such as Environment.expertPath or Environment.networkSharePath
    if ([string]::IsNullOrEmpty($xpath)) {
        Write-Host -ForegroundColor Yellow "You need to specify an xpath expression";
        return $null;
    }
    if (Test-Path variable:global:BranchBinariesDirectory) {
        $environmentXmlPath = [System.IO.Path]::Combine($global:BranchBinariesDirectory, "environment.xml");
        [xml]$xml = Get-Content $environmentXmlPath;
        $returnValue = Select-Xml $xpath $xml;
        return $returnValue;
    } else {
        Write-Host -ForegroundColor Yellow "I don't know where your Branch Binaries Directory is.";
    }
    return $null;
}

<#
.Synopsis
    Start de remove; de deploy; for the current branch
.Description
    Will redeploy your Expert Environment for the current branch. This command beeps twice when it has finished.
.PARAMETER getDependencies
    Will get dependencies for the current selected module.
.PARAMETER GetLatestForBranch
    Will get the latest source code for your current branch.
.PARAMETER GetProduct
    Will retrieve the latest product binaries before starting the new deployment.
.PARAMETER GetProductZip
    Will retrieve the latest build all product zip binaries before starting the new deployment. (if you specify both -GetProduct and -GetProductZip then you will get the zip file)
.PARAMETER upd
    Will run Update-Database before starting the new deployment.
.PARAMETER CopyBinariesFrom
    Copy binaries from the bin folder of these specified modules into expert source. If you combine this with -GetProduct, this will happen AFTER getting the latest product.
.EXAMPLE
        Start-Redeployment -getDependencies -GetLatestForBranch -GetProduct -CopyBinariesFrom Applications.Deployment, Libraries.Deployment
    Will first get latest source code for the branch,
    get dependencies for current module,
    remove your existing deployment,
    get product,
    Copy binaries from Applications.Deployment,
    and finally start your new deployment.
#>
function Start-Redeployment([switch]$GetProduct, [switch]$GetProductZip, [switch]$upd, [switch]$dontKillRunning, [switch]$GetLatestForBranch, [switch]$getDependencies, $CopyBinariesFrom) {
    $start = get-date
    if ($GetLatestForBranch) {
        Get-Latest -branch
    }
    if ($getDependencies) {
        if(!($global:CurrentModuleName)){
            Write-Warning "No current module is set. Cannot get dependencies"
        } else {
            Get-DependenciesForCurrentModule
        }
    }
    if (-not $dontKillRunning) {
        $running = Get-Process
        foreach ($item in Get-Item -Filter *.exe -Path C:\ExpertShare\*) {
            foreach($process in $running) {
                if ($process.name -ieq $item.BaseName) {
                    $name = $item.BaseName
                    Write-Host "Attempting to kill $name as it still appears to be running."
                    Stop-Process -Name $item.BaseName -Force
                }
            }
        }
    }
    de remove;
    if ($GetProductZip) {
        Get-ProductZip
    } elseif ($GetProduct) {
        Get-Product
    }
    if (-not [String]::IsNullOrEmpty($CopyBinariesFrom)) {
        $startModule = Get-CurrentModule
        foreach ($module in $CopyBinariesFrom) {
            Set-CurrentModule -quiet $module
            Write-Host -ForegroundColor White -NoNewline "Copying binaries from: "
            Write-Host -ForegroundColor DarkCyan -NoNewline $module
            Copy-BinariesFromCurrentModule
            Write-Host -ForegroundColor White "Done."
        }
        Set-CurrentModule -quiet $startModule
    }
    #Could we add restore DB here?
    if ($upd) {
        Update-Database
    }
    de deploy
    $end = get-date
    $start
    $end
    $end-$start
    Get-Beep; Get-Beep #Audio feedback that we have finished.
}

function Get-Beep() {
    $([char]7)
}

function tryToRemove ($path){
    if(Test-Path $path){
        Remove-Item $path -recurse -Force;
    }
}

<#
.Synopsis
    Cleans the files from the web modules which are not in the source control.
.Description
    Following files will be deleted in the module:
        Dependencies\*
        Bin\*
        Src\$ModuleName\bin\*
    Also other files could be removed if scorch flag is on.
.PARAMETER ModuleNames
    An array of module names for which you want to clean.
.PARAMETER Scorch
    Use this switch if you want to do a scorch as well.
.EXAMPLE
    Clean Web.Presentation, Web.Foundation -Scorch
#>
function Clean($moduleNames = $global:CurrentModuleName, [switch]$scorch) {
    foreach ($moduleName in $moduleNames) {
        $path = Join-Path $global:BranchLocalDirectory "Modules\$ModuleName"

        tryToRemove $path\Dependencies\*
        tryToRemove $path\Bin\*
        tryToRemove $path\Src\$ModuleName\bin\*
    }
    if ($Scorch){
        Scorch $moduleNames;
    }
}

<#
.Synopsis
    Scorch the given modules.
.PARAMETER ModuleNames
    An array of module names for which you want to scorch.
.EXAMPLE
    Scorch Web.Presentation, Web.Foundation
#>
function Scorch($moduleNames = $global:CurrentModuleName) {
        foreach ($moduleName in $moduleNames) {
            $path = Join-Path $global:BranchLocalDirectory "Modules\$ModuleName"
            invoke-expression "tfpt scorch $path /recursive /noprompt";
        }
}


function InstallPester() {
    Write-Debug "Installing Pester"

    $dir = "cmd /c rmdir `"$env:USERPROFILE\Documents\WindowsPowerShell\Modules\Pester`""
    Invoke-Expression $dir

    if ($BranchLocalDirectory -ne $null) {
        $expression = "cmd /c mklink /d `"$env:USERPROFILE\Documents\WindowsPowerShell\Modules\Pester`" `"$BranchLocalDirectory\Modules\Build.Infrastructure\Src\Profile\Pester\`""
        Invoke-Expression $expression
    }
}


function Help ($searchText) {

    if($searchText){
        $searchText = "*$searchText*";
        foreach ($func in $helpList) {
            $functionName = $func.function;
            $aliasName = $func.alias;

            if(($functionName -like $searchText)  -or ($aliasName -like $searchText)){
                Write-Host -ForegroundColor Green -NoNewline "$functionName, $aliasName ";
                Write-Host (Get-Help $functionName).Synopsis;
            }
        }
        return;
    }

    $AderantModuleLocation = Get-AderantModuleLocation
    Write-Host "Using Aderant Module from : $AderantModuleLocation"
    Write-Host ""
    Write-Host "The following aliases are defined: "
    Write-Host ""

    $sortedFunctions = $helpList | Sort-Object -Property Alias -Descending
    $sortedFunctions | Format-Table Command, Synopsis

    Write-Host ""
    Write-Host "Also note that module and branch names will auto-complete when pressing tab"
    Write-Host ""
}

function Show-RandomTip {
    $randomIndex = Get-Random -minimum 0 -maximum ($helpList.length-1);
    $functionToTip = $helpList[$randomIndex];
    $functionToTipName = $functionToTip.function;
    $functionToTipAlias = $functionToTip.alias
    Write-Host -ForegroundColor Green "$functionToTipName, $functionToTipAlias";
    Get-Help $functionToTipName
    Write-Host -ForegroundColor Green "--------------------------------------------------------------------------------------------------"
}

<#
.SYNOPSIS
Looks for web applications registered in IIS that do not map to the file system

.PARAMETER virtualPath
Virtual path to search within

.EXAMPLE
Hunt-Zombies -VirtualPath 'Expert_Local'

Finds all the zombie applications in Expert_Local 
#>
function Hunt-Zombies {
    param(
        [Parameter(Mandatory=$false)] [string] $virtualPath = ''
    )
    if(-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
        Write-Warning "You do not have Administrator rights to run this script`nPlease re-run this script as an Administrator"
        Break
    }

    Import-Module WebAdministration
    Import-Module ApplicationServer

    if([String]::IsNullOrWhitespace($virtualPath)) {
        $expertWebApplications = get-ASApplication -SiteName 'Default Web Site'
    } else {
        $expertWebApplications = get-ASApplication -SiteName 'Default Web Site' -VirtualPath $virtualPath
    }
	
    foreach($webApp in $expertWebApplications){
        if(-not ((Test-Path $webApp.IISPath) -band (Test-Path $($webApp.PhysicalPath)))) {
			if($webApp.ApplicationName) {
			    $iisPath = $webApp.IISPath
			    $filePath = $webApp.PhysicalPath
                Write-Output "Found zombie web application $iisPath, could not find path $filePath."
            }    
	    }
    }    
	Write-Output 'Zombie hunt complete.'
}

<#
.SYNOPSIS
Removes web applications registered in IIS that do not map to the file system

.PARAMETER virtualPath
Virtual path to search within

.EXAMPLE
Remove-Zombies -VirtualPath 'Expert_Local'

Removes all the zombie applications in Expert_Local 
#>
function Remove-Zombies {
    param(
        [Parameter(Mandatory=$false)] [string] $virtualPath = ''
    )

	if(-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
        Write-Warning "You do not have Administrator rights to run this script`nPlease re-run this script as an Administrator"
        Break
    }

    Import-Module WebAdministration
    Import-Module ApplicationServer

    if([String]::IsNullOrWhitespace($virtualPath)) {
        $expertWebApplications = get-ASApplication -SiteName 'Default Web Site'
    } else {
        $expertWebApplications = get-ASApplication -SiteName 'Default Web Site' -VirtualPath $virtualPath
    }
	
    foreach($webApp in $expertWebApplications){
        if(-not ((Test-Path $webApp.IISPath) -band (Test-Path $($webApp.PhysicalPath)))) {
			if($webApp.ApplicationName) {
			    $iisPath = $webApp.IISPath
                Remove-Item -Path $iisPath
			    Write-Output "Removed zombie web application $iisPath"
			}
        }
	}
	Write-Output 'Zombie removal complete.'
}

<#
.Synopsis
    Backs up the database for the current local deployment.
.Description
    Backs up the database for the current local deployment.
.PARAMETER databaseServer
    The database server\instance the database is on.
.PARAMETER database
    The database to backup.
.PARAMETER backupPath
    The directory to backup the database to. Defaults to C:\AderantExpert\DatabaseBackups.
.PARAMETER database
    The name for the database backup. Defaults to the name of the database to back up.
.PARAMETER provisionDB
    Runs ProvisionDB.sql prior to backing up the database.
.EXAMPLE
        Backup-ExpertDatabase -databaseServer SVSQL306 -database ExpertDatabase -backupPath C:\Test -backupName TestBackup
    Will backup the ExpertDatabase on SVSQL306 to C:\Test\TestBackup.bak
#>
function Backup-ExpertDatabase () {
	param(
		[string]$databaseServer, [string]$database, [string]$backupPath = "C:\AderantExpert\DatabaseBackups", [string]$backupName, [switch]$provisionDB
	)

    if ([string]::IsNullOrWhiteSpace($databaseServer)) {
        $databaseServer = Get-DatabaseServer
    }

    if ([string]::IsNullOrEmpty($database)) {
        $database = Get-Database
    }

	if (-not [string]::IsNullOrWhiteSpace($backupName)) {
		[string]$backup = "$backupPath\$backupName.bak"
	}

    try { 
        if (-not (Test-Path $backupPath)) {
		    try {
			    New-Item $backupPath -Type Directory
                Write-Debug "Successfully created directory $backupPath"
		    } catch {
			    Write-Error "Unable to create directory at $backupPath"
                return
		    }
	    }
    } catch {
        Write-Error "Invalid backup path: $backupPath"
        return
    }

    if ($provisionDB.IsPresent) {
        try {
			Write-Host "Getting latest on Tests.UIAutomation module."
            Get-Latest Tests.UIAutomation
        } catch {
            Write-Error "Unable to update Tests.UIAutomation module."
        }
        try {
            [string]$provisionDBPath = "$global:BranchModulesDirectory\Tests.UIAutomation\Src\Tests.UIAutomation.Expert\Resources\ProvisionDB.sql"
            if (-not (Test-Path $provisionDBPath)) {
                Write-Error "Unable to locate ProvisionDB.sql at $provisionDBPath"
            }
            [System.Array]$provisionDBFile = Get-Content $provisionDBPath
			[string]$provisionDBScript = "USE [$database];`n"
			foreach ($line in $provisionDBFile) {
				$provisionDBScript = "$provisionDBScript$line`n"
			}
			Out-File -FilePath "$backupPath\ProvisionDB.sql" -InputObject $provisionDBScript -Force
        } catch {
            Write-Error "Unable to process ProvisionDB.sql successfully."
            return
        }
        try {
            Invoke-SqlCmd -ServerInstance $databaseServer -InputFile "$backupPath\ProvisionDB.sql" -ErrorAction Stop -QueryTimeout 60
        } catch {
            Write-Error "Unable to run ProvisionDB successfully."
			return
        }
		Remove-Item -Path "$backupPath\ProvisionDB.sql" -Force
		if ([string]::IsNullOrWhiteSpace($backup)) {
			[string]$backup = "{0}\{1}ProvisionDB.bak" -f $backupPath, $database
		}
		Write-Host "Successfully ran ProvisionDB.sql on $database database."
    }

	if ([string]::IsNullOrWhiteSpace($backup)) {
		[string]$backup = "$backupPath\$database.bak"
	}

    try {
        [string]$backupScript = "
USE master;
BACKUP DATABASE [$database] TO DISK = `'$backup`' WITH FORMAT, COMPRESSION;
GO"
        Invoke-SqlCmd -ServerInstance $databaseServer -Query $backupScript -ErrorAction Stop -QueryTimeout 300
	    Write-Host "$database backed up successfully to $backup"
    } catch {
        Write-Host $backupScript
	    Write-Error "Exception thrown while backing up $database."
    }
}

<#
.Synopsis
    Restores the database for the current local deployment.
.Description
    Restores the database for the current local deployment.
.PARAMETER databaseServer
    The database server\instance the database is on.
.PARAMETER database
    The name of the database to restore. Defaults to the current Expert database backup at \\[Computer_Name]\C$\AderantExpert\DatabaseBackups.
.PARAMETER backup
    The database backup to restore. Defaults to \\[Computer_Name]\C$\AderantExpert\DatabaseBackups\[database_name].bak.
.PARAMETER provisionDB
    Indicates that the backup with ProvisionDB run will be restored.
.EXAMPLE
        Restore-ExpertDatabase -databaseServer SVSQL306 -database Expert -backup C:\Test\DatabaseBackup.bak
    Will restore the Expert database on to the SVSQL306 SQL server.
#>
function Restore-ExpertDatabase {
	param(
		[string]$databaseServer, [string]$database, [string]$backup, [switch]$provisionDB
	)

    if ([string]::IsNullOrWhiteSpace($databaseServer)) {
        $databaseServer = Get-DatabaseServer
    }

    if ([string]::IsNullOrEmpty($database)) {
        $database = Get-Database
    }

    if ([string]::IsNullOrWhiteSpace($backup)) {
		if ($provisionDB.IsPresent) {
			$backup = "\\$env:COMPUTERNAME\C$\AderantExpert\DatabaseBackups\{0}ProvisionDB.bak" -f $database
		} else {
			$backup = "\\$env:COMPUTERNAME\C$\AderantExpert\DatabaseBackups\$database.bak"
		}
    }

    if (-Not (Test-Path $backup)) {
        Write-Error "Invalid backup file at: $backup"
        return
    }

    try {
        [string]$restoreScript = "
USE master;
ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [$database] FROM DISK = '$backup' WITH REPLACE, STATS = 10;
ALTER DATABASE [$database] SET NEW_BROKER WITH ROLLBACK IMMEDIATE;
ALTER DATABASE [$database] SET MULTI_USER With ROLLBACK IMMEDIATE;
GO"
        Invoke-SqlCmd -ServerInstance $databaseServer -Query $restoreScript -ErrorAction Stop -QueryTimeout 300
	    Write-Host "$database restored successfully"
    } catch [System.Exception] {
        Write-Host $restoreScript
	    Write-Error "Exception thrown while backing up $database."
    }
}


<#
.Synopsis
    Returns the Database Server\Instance for the current local deployment.
.Description
    Uses Get-EnvironmentFromXml to return the Database Server\Instance for the current local deployment.
#>
function Get-DatabaseServer() {
    try {
        [string]$databaseServer = [regex]::Match((Get-EnvironmentFromXml "/environment/expertDatabaseServer/@serverName"), "[^/]*$").ToString()
        Write-Debug "Database server set to: $databaseServer"
    } catch {
        throw "Unable to get database server from environment.xml"
    }

    try {
        [string]$serverInstance = [regex]::Match((Get-EnvironmentFromXml "/environment/expertDatabaseServer/@serverInstance"), "[^/]*$").ToString()
        if (-not [string]::IsNullOrWhiteSpace($serverInstance) ) {
            $databaseServer = "$databaseServer\$serverInstance"
            Write-Host "Database server instance set to: $databaseServer"
        }
    } catch [System.Exception] {
        throw "Unable to get database server instance from environment.xml"
    }

    return $databaseServer
}

<#
.Synopsis
    Returns the database name for the current local deployment.
.Description
    Uses Get-EnvironmentFromXml to return the the database name for the current local deployment.
#>
function Get-Database() {
    try {
        [string]$database = [regex]::Match((Get-EnvironmentFromXml "/environment/expertDatabaseServer/databaseConnection/@databaseName"), "[^/]*$").ToString()
        Write-Debug "Database name set to: $database"
    } catch [System.Exception] {
        throw "Unable to get database name from environment.xml"
    }

    return $database
}

# export functions and variables we want external to this script
$functionsToExport = @(
	[pscustomobject]@{ function='Backup-ExpertDatabase';                      alias='dbbak'},
    [pscustomobject]@{ function='Branch-Module';                              alias='branch'},
    [pscustomobject]@{ function='Build-ExpertModules';                        alias='bm'},
    [pscustomobject]@{ function='Build-ExpertModulesOnServer';                alias='bms'},
    [pscustomobject]@{ function='Build-ExpertPatch';},
    [pscustomobject]@{ function='Change-Directory';                           alias='cdir'},
    [pscustomobject]@{ function='Copy-BinariesFromCurrentModule';             alias='cb'},
    [pscustomobject]@{ function='Disable-ExpertPrompt';                       advanced=$true},
    [pscustomobject]@{ function='Enable-ExpertPrompt';                        advanced=$true},
    [pscustomobject]@{ function='Generate-SystemMap'},
    [pscustomobject]@{ function='Get-AderantModuleLocation';                  advanced=$true},
    [pscustomobject]@{ function='Get-Beep';                                   alias='beep'},
    [pscustomobject]@{ function='Get-CurrentModule'},
    [pscustomobject]@{ function='Get-DependenciesForCurrentModule';           alias='gd'},
    [pscustomobject]@{ function='Get-DependenciesForEachModule';              alias='gde'},
    [pscustomobject]@{ function='Get-DependenciesFrom';                       alias='gdf'},
    [pscustomobject]@{ function='Get-EnvironmentFromXml'},
    [pscustomobject]@{ function='Get-ExpertModulesInChangeset'},
    [pscustomobject]@{ function='Get-Latest'},
    [pscustomobject]@{ function='Get-LocalDependenciesForCurrentModule';      alias='gdl'},
    [pscustomobject]@{ function='Get-Product'},
    [pscustomobject]@{ function='Get-ProductNoDebugFiles'},
	[pscustomobject]@{ function='Get-ProductBuild';                           alias='gpb'},
    [pscustomobject]@{ function='Get-ProductZip'},
    [pscustomobject]@{ function='Help'},
    [pscustomobject]@{ function='Install-LatestSoftwareFactory';              alias='usf'},
    [pscustomobject]@{ function='Install-LatestVisualStudioExtension'},
    [pscustomobject]@{ function='Kill-VisualStudio';                          alias='kvs'},
    [pscustomobject]@{ function='Move-Shelveset';},
    [pscustomobject]@{ function='New-BuildModule';},
    [pscustomobject]@{ function='Open-Directory';                             alias='odir'},
    [pscustomobject]@{ function='Open-ModuleSolution';                        alias='vs'},
	[pscustomobject]@{ function='Restore-ExpertDatabase';},
    [pscustomobject]@{ function='Set-CurrentModule';                          alias='cm'},
    [pscustomobject]@{ function='Set-Environment';                            advanced=$true},
    [pscustomobject]@{ function='Set-ExpertBranchInfo';},
    [pscustomobject]@{ function='Show-RandomTip';                             alias='tip'},
    [pscustomobject]@{ function='Start-dbgen';                                alias='dbgen'},
    [pscustomobject]@{ function='Start-DeploymentEngine';                     alias='de'},
    [pscustomobject]@{ function='Start-DeploymentManager';                    alias='dm'},
    [pscustomobject]@{ function='Start-Redeployment';                         alias='redeploy'},
    [pscustomobject]@{ function='SwitchBranchTo';                             alias='Switch-Branch'},
	[pscustomobject]@{ function='Prepare-Database';                           alias='dbprep'},
    [pscustomobject]@{ function='Update-Database';                            alias='upd'},
    [pscustomobject]@{ function='Scorch';},
    [pscustomobject]@{ function='Clean';},
    [pscustomobject]@{ function='Hunt-Zombies';                               alias='hz'},
    [pscustomobject]@{ function='Remove-Zombies';                             alias='rz'}
)

$helpList = @()

# Exporting the functions and aliases
foreach ($toExport in $functionsToExport) {
    Export-ModuleMember -function $toExport.function

    if ($toExport.alias) {
        Set-Alias $toExport.alias  $toExport.function
        Export-ModuleMember -Alias $toExport.alias
    }

    if (-not $toExport.advanced) {
        # building up a help list to show in the RandomTip function and also the help function
        $ast = (Get-Command $toExport.function).ScriptBlock.Ast
        if ($ast) {
            $help = $ast.GetHelpContent()
        }

        if ($toExport.alias) {
            $helpList += [pscustomobject]@{Command=$toExport.alias; Alias=$toExport.Alias; Synopsis=$help.Synopsis}
        } else {
            $helpList += [pscustomobject]@{Command=$toExport.function; Alias=$null; Synopsis=$help.Synopsis}
        }
    }
}

#also include all the csharp commandlets in the help list
# TODO: This is quite slow
#foreach($csharpCommandlet in Get-Command -Module 'dynamic_code_module_Aderant.Build, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null') {
    #$help = (Get-Help $csharpCommandlet)
    #$helpList += [pscustomobject]@{Command=$csharpCommandlet.Name; Alias=$null; Synopsis=$help.Synopsis}
#}

# paths
Export-ModuleMember -variable CurrentModuleName
Export-ModuleMember -variable BranchServerDirectory
Export-ModuleMember -variable BranchLocalDirectory
Export-ModuleMember -variable CurrentModulePath
Export-ModuleMember -variable BranchBinariesDirectory
Export-ModuleMember -variable BranchName
Export-ModuleMember -variable BranchModulesDirectory
Export-ModuleMember -variable ProductManifestPath

Enable-ExpertPrompt

Check-DeveloperTools

Set-Environment