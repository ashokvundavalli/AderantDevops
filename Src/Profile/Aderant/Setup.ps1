$currentDirectory = pwd
$psHomeDirectory = Join-Path $Env:UserProfile "Documents\WindowsPowerShell\"
$aderantModuleDirectory = Join-Path $psHomeDirectory "Modules\Aderant"

$moduleExists = Test-Path $aderantModuleDirectory

if (-not $moduleExists) {	
    # Find ExpertSuite MAIN and create the appropriate symlink
    [Reflection.Assembly]::Load("Microsoft.TeamFoundation, Version=11.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a") | Out-Null
    [Reflection.Assembly]::Load("Microsoft.TeamFoundation.Client, Version=11.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a") | Out-Null
    [Reflection.Assembly]::Load("Microsoft.TeamFoundation.VersionControl.Client, Version=11.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a") | Out-Null

    $uri = New-Object System.Uri("http://tfs:8080/tfs/")

    $server = [Microsoft.TeamFoundation.Client.TfsTeamProjectCollectionFactory]::GetTeamProjectCollection($uri)
    $server.EnsureAuthenticated()

    foreach ($info in [Microsoft.TeamFoundation.VersionControl.Client.Workstation]::Current.GetAllLocalWorkspaceInfo()) {
        $workspace = $info.GetWorkspace($server)
        $folder = $workspace.TryGetWorkingFolderForServerItem("$/ExpertSuite/dev/BuildMods/Modules/Build.Infrastructure");

        if ($folder -ne $null) {
            $cmd = "cmd /c mklink " + $aderantModuleDirectory + " " + $folder.LocalItem + "\Src\Profile\Aderant /D"            
            Invoke-Expression $cmd
            break
        }
    }
}