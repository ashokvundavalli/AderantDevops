<#
    Provides a context object that stores state for when the build tool chain is running interactively
#>
class ShellContext {
    hidden $autoProperties = @{
        'Version' = 1.0
        'IsTfvcModuleEnabled' = $false
    }

    hidden [string] $configFile = $null

    ShellContext() {
        $this.ProfileHome = [System.IO.Path]::Combine([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData), "Aderant", "ContinuousDelivery")

        if (-not (Test-Path $this.ProfileHome)) {
            New-Item -Type Directory -Path $this.ProfileHome -ErrorAction SilentlyContinue | Out-Null
        }

        $this.configFile = [System.IO.Path]::Combine($this.ProfileHome, "config.json")
        $this.AddPublicMembers()

        if (Test-Path $this.configFile) {
            $data = Get-Content -Path $this.configFile | ConvertFrom-Json
            $data.psobject.properties | Foreach { $this.autoProperties[$_.Name] = $_.Value }
        }
    }

    hidden AddPublicMembers() {
        $Members = $this.autoProperties.Keys

        foreach ($Member in $Members) {
            $PublicPropertyName = $Member -replace '_', ''
            # Define getter part
            $Getter = 'return $this.autoProperties["{0}"]' -f $Member
            $Getter = [ScriptBlock]::Create($Getter)

            # Define setter part
            $Setter = '
$this.autoProperties["{0}"] = $args[0]
$this.autoProperties | ConvertTo-Json | Out-File {1}
' -f $Member, $this.configFile
            $Setter = [ScriptBlock]::Create($Setter)

            $AddMemberParams = @{
                Name = $PublicPropertyName
                MemberType = 'ScriptProperty'
                Value = $Getter
                SecondValue = $Setter
            }
            $this | Add-Member @AddMemberParams
        }
    }

    [string] $BuildScriptsDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, "..\..\Build"))
    [string] $BuildToolsDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, "..\..\Build.Tools"))
    [string] $PackagingTool = [System.IO.Path]::Combine($this.BuildScriptsDirectory, "paket.exe")
    [string] $ProfileHome
    [string] $RegistryHome

    [string] $BranchRoot = ""
    [string] $BranchName = ""
    [string] $BranchLocalDirectory = ""
    [string] $BranchServerDirectory = ""
    [string] $BranchModulesDirectory = ""
    [string] $BranchBinariesDirectory = ""
    [string] $BranchExpertSourceDirectory = ""
    [string] $BranchExpertVersion = ""
    [string] $BranchEnvironmentDirectory = ""

    [string] $PackageScriptsDirectory = ""
    [string] $ModuleCreationScripts = ""
    [string] $ProductManifestPath = ""
    [string] $CurrentModuleName = ""
    [string] $CurrentModulePath = ""
    [string] $CurrentModuleBuildPath = ""

    [bool] $IsGitRepository = $false
    [bool] $PoshGitAvailable = $false
}