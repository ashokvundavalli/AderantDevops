param(
)



task Build {

}

# Gets *.tmp files from the temp directory. The list is used by two tasks.
$GetTmpFiles = gci $PSScriptRoot\Src\Build.Tools\ -Recurse -File


task BuildPowerShellFarHelp -Inputs $GetTmpFiles -Outputs "$PSScriptRoot\PowerShellFar.dll-Help.xml" {
    Add-Type -Path $PSScriptRoot\src\Build.Tools\Aderant.Build.dll
    
    $ps = [Management.Automation.PowerShell]::Create()
    $state = [Management.Automation.Runspaces.InitialSessionState]::CreateDefault()
    [PowerShellFar.Zoo]::Initialize($state)
    $ps.Runspace = [Management.Automation.Runspaces.RunspaceFactory]::CreateRunspace($state)
    $ps.Runspace.Open()
    #! $ErrorActionPreference = 1 in Convert-Helps does not help to catch errors
    
    $null = $ps.AddScript(@"
`$ErrorActionPreference = 1
. Helps.ps1
Convert-Helps "$BuildRoot\Commands\PowerShellFar.dll-Help.ps1" "$Outputs"
"@)
    $ps.Invoke()
}




# Install all. Run after Build.
task . Build, BuildPowerShellFarHelp