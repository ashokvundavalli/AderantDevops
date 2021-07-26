<#
    Git must be configured to pass our Windows credentials or the tool will prompt for user/password input and so will hang indefinitely
#>
$ErrorActionPreference = 'Stop';

[System.Environment]::SetEnvironmentVariable('GIT_REDIRECT_STDERR', '2>&1', [System.EnvironmentVariableTarget]::Process);
[string]$pathToGit = "$Env:ProgramFiles\Git\cmd\git.exe";

if (-not (Test-Path $pathToGit)) {
    throw "Git.exe not found at $pathToGit";
}

[string[]]$hosts = @("tfs", "tfs.ap.aderant.com", "tfs.aderant.com");

# Host is a reserved readonly PowerShell variable and so cannot be assigned to.
foreach ($entry in $hosts) {
    & $pathToGit config --global credential.$entry.interactive never;
    & $pathToGit config --global credential.$entry.integrated true;
}

& $pathToGit config --global http.emptyAuth true;
& $pathToGit config --global credential.authority ntlm;