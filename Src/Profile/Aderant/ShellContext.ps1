<#
    Provides a context object that stores state for when the build tool chain is running interactively 
#>
class ShellContext {
    ShellContext() {
        $path = "HKCU:\Software\Aderant\PowerShell"
        if (-not (Test-Path $path)) {
            New-Item -Path $path -ErrorAction SilentlyContinue | Out-Null 
        }
        $this.RegistryHome = $path 

        # Create the path to the cache if it does not exist
        New-Item -Path $this.CacheDirectory -ItemType Directory -Force -ErrorAction SilentlyContinue | Out-Null
    }
        
    [string] $BuildScriptsDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, "..\..\Build"))
    [string] $BuildToolsDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, "..\..\Build.Tools"))
    [string] $PackagingTool = [System.IO.Path]::Combine($this.BuildScriptsDirectory, "paket.exe")
    [string] $CacheDirectory = [System.IO.Path]::Combine([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData), "AderantPowerShell")
    [string] $CurrentCommit
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

    [bool] $IsGitRepository 
    [bool] $PoshGitAvailable

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