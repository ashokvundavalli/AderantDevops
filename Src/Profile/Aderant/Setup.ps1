$currentDirectory = pwd
$psHomeDirectory = Join-Path $Env:UserProfile "Documents\WindowsPowerShell\"
$moduleDirectory = Join-Path $psHomeDirectory "Modules"

if (-not (Test-Path "$moduleDirectory\Aderant")) {	
    New-Item -ItemType Directory -Path $moduleDirectory -Force -ErrorAction SilentlyContinue

    # Find ExpertSuite MAIN and create the appropriate symlink
    [Reflection.Assembly]::Load("Microsoft.TeamFoundation, Version=11.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a") | Out-Null
    [Reflection.Assembly]::Load("Microsoft.TeamFoundation.Client, Version=11.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a") | Out-Null
    [Reflection.Assembly]::Load("Microsoft.TeamFoundation.VersionControl.Client, Version=11.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a") | Out-Null

    $uri = New-Object System.Uri("http://tfs:8080/tfs/")

    $server = [Microsoft.TeamFoundation.Client.TfsTeamProjectCollectionFactory]::GetTeamProjectCollection($uri)
    $server.EnsureAuthenticated()

    foreach ($info in [Microsoft.TeamFoundation.VersionControl.Client.Workstation]::Current.GetAllLocalWorkspaceInfo()) {
        $workspace = $info.GetWorkspace($server)
        $folder = $workspace.TryGetWorkingFolderForServerItem("$/ExpertSuite/Main/Modules/Build.Infrastructure");

        if ($folder -ne $null) {
            $cmd = "cmd /c mklink " + "$moduleDirectory\Aderant" + " " + $folder.LocalItem + "\Src\Profile\Aderant /D"            
            Invoke-Expression $cmd
            
            # Copy and setup _profile script
            $script = Join-Path $folder.LocalItem -ChildPath "\Src\Profile\WindowsPowerShell\Microsoft.PowerShell_profile.ps1"
            Copy-Item $script $psHomeDirectory
        }
    }


}