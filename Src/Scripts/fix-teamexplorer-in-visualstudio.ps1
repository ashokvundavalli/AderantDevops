# This script attempts to fix the Git/TeamExplorer integration in Visual Studio 2017.
# Often the private registry hive and the TeamExplorer.config end up with corrupted entries with prevent the Team Explorer features from working such as 
# the build and work item tabs.
# The script will scan your source directory and attempt to repair the regsitry and TeamExplorer files. VS 2017 must be clsoed for this to work as VS places an exclusive lock
# on the hive

$repoRoot = "C:\Source\"
$vsFolderName = "15.0_e6d624ed"
$tfsUrl = "http://tfs:8080/Aderant/ExpertSuite"
$allRepositories = Invoke-RestMethod -Uri "$tfsUrl/_apis/git/repositories/" -UseDefaultCredentials


# This function duplicates the logic found in various internal VS dlls. Microsoft take the path of the repository and SHA1 that to produce a unique repository key
if (-not ([System.Management.Automation.PSTypeName]'RepositoryHelper').Type) {

$Source = @"
public static class RepositoryHelper {
    public static string GetRepositoryKeyName(string repositoryPath)
    {
	    byte[] value;
	    using (var memoryStream = new System.IO.MemoryStream(System.Text.Encoding.Unicode.GetBytes(repositoryPath.TrimEnd(new char[]
	    {
		    System.IO.Path.DirectorySeparatorChar
	    }).ToLower(System.Globalization.CultureInfo.InvariantCulture))))
	    {
		    using (var sHA =  System.Security.Cryptography.SHA1.Create())
		    {
			    value = sHA.ComputeHash(memoryStream);
		    }
	    }
	    return System.BitConverter.ToString(value).Replace("-", "");
    }
}
"@ 

    Add-Type -ReferencedAssemblies "mscorlib", "System" -TypeDefinition $Source -Language CSharp 
}


$teamExplorerConfigFile = "$env:APPDATA\Microsoft\VisualStudio\$vsFolderName\Team Explorer\TeamExplorer.config"
$teamExplorerXml = [xml] (Get-content -Path $teamExplorerConfigFile)


$repos = gci -Path $repoRoot -Directory -Depth 0

try {
    $drive = Get-PSDrive -Name HKU
    if (-not $drive) {
        New-PSDrive HKU Registry HKEY_USERS
    }

    reg load 'HKU\VS2017PrivateRegistry\' "$env:LOCALAPPDATA\Microsoft\VisualStudio\$vsFolderName\privateregistry.bin"


    foreach ($repo in $repos) {
        $path = [System.IO.Path]::Combine($repoRoot, $repo.Name)

        if (Test-Path $path) {
            $newKey = [Hash]::GetRepositoryKeyName($path)

            if (-not (Test-Path "Registry::HKU\VS2017PrivateRegistry\Software\Microsoft\VisualStudio\$vsFolderName\TeamFoundation\GitSourceControl\Repositories\")) {
                New-Item "Registry::HKU\VS2017PrivateRegistry\Software\Microsoft\VisualStudio\$vsFolderName\TeamFoundation\GitSourceControl\Repositories\"
            }

            $registryKey = "Registry::HKU\VS2017PrivateRegistry\Software\Microsoft\VisualStudio\$vsFolderName\TeamFoundation\GitSourceControl\Repositories\$newKey"        

            if (-not (Test-Path $registryKey)) {
                New-Item -Path $registryKey
                New-ItemProperty -Path $registryKey -Name "Name" -Value $repo.name -PropertyType String | Out-Null
                New-ItemProperty -Path $registryKey -Name "OriginRemoteUrl" -Value "$tfsUrl/_git/$($repo.Name)"  -PropertyType String | Out-Null
                New-ItemProperty -Path $registryKey -Name "Path" -Value $path -PropertyType String | Out-Null
            } else {
                Write-Host "Entry already exists: $path"
            }
            
            # Fix up TeamExplorer.config
            $teamExplorerEntry = $teamExplorerXml.server_list.server.collection.project.GetElementsByTagName("repository") | where {$_.name -eq $repo.name }
            if (-not $teamExplorerEntry) {
                $match = $allRepositories.value | Where-Object { $_.name -eq $repo.name } | Select-Object -First 1
             
                if ($match) {                  
                    $repositoryElement = $teamExplorerXml.CreateElement("repository")
                    $repositoryElement.Attributes.Append($teamExplorerXml.CreateAttribute("type")).Value = "2"
                    $repositoryElement.Attributes.Append($teamExplorerXml.CreateAttribute("name")).Value = $repo.name
                    $repositoryElement.Attributes.Append($teamExplorerXml.CreateAttribute("guid")).Value = $match.id           
                    $repositoryElement.Attributes.Append($teamExplorerXml.CreateAttribute("isFork")).Value = $false

                    $teamExplorerXml.server_list.server.collection.project.AppendChild($repositoryElement)
                    $teamExplorerXml.Save($teamExplorerConfigFile)
                }
            } 
        }    
    }

} finally {
    [GC]::Collect()
    reg unload 'HKU\VS2017PrivateRegistry'
}