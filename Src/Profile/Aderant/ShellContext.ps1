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
        $path = "HKCU:\Software\Aderant\PowerShell"
        if (-not (Test-Path $path)) {
            New-Item -Path $path -ErrorAction SilentlyContinue | Out-Null 
        }
        $this.RegistryHome = $path       

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

    [object] SetRegistryValue([string]$path, [string]$name, $value) {
        $fullPath = ($this.RegistryHome + "\" + $path).TrimEnd("\")
        if (-not (Test-Path $fullPath)) {
            Write-Debug "Creating path: $fullPath"
            New-Item -Path $this.RegistryHome -Name $path -Force
        }  

        Write-Debug "Updating key: $fullPath\$name"
        $key = New-ItemProperty -Path $fullPath -Name $name -Value $value -Force

        return $key
    }

    [object] GetRegistryValue([string]$path, [string]$name) {
        if ([string]::IsNullOrWhitespace($path)) {
          $path = [string]::Empty
        }
        
        $fullPath = ($this.RegistryHome + "\" + $path).TrimEnd("\")
        
        Write-Debug "Retrieving value: $fullPath\$name"
        
        if (Test-Path -Path $fullPath) {
          try {
            $value = Get-ItemPropertyValue -Path $fullPath -Name $name -ErrorAction SilentlyContinue
            Write-Debug $value
            return $value
          } catch {
            # Property/value may not exist
          }
        }

        return $null
    }
}