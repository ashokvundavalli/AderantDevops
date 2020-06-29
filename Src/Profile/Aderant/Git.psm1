$currentUser = [Security.Principal.WindowsPrincipal]([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdminProcess = $currentUser.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$adminHeader = if ($isAdminProcess) { 'Administrator: ' } else { '' }

function global:Enable-GitPrompt {
    Function global:Prompt {
        $realLASTEXITCODE = $LASTEXITCODE

        [string]$location = Get-Location
        
        Write-Host ([string]::Empty)
        Write-Host 'Module [' -NoNewline
        Write-Host ($global:ShellContext.CurrentModuleName) -NoNewline -ForegroundColor DarkCyan
        Write-Host '] at [' -NoNewline
        Write-Host ($global:ShellContext.CurrentModulePath) -NoNewline -ForegroundColor DarkCyan
        Write-Host ']'
        Write-Host "PS ${location}" -NoNewline

        if ($global:ShellContext.PoshGitAvailable) {
            Write-VcsStatus

            $status = Get-GitStatus

            if ($null -ne $status) {
                $repoName = Split-Path -Leaf (Split-Path $status.GitDir)
                $Host.UI.RawUI.WindowTitle = "$script:adminHeader$repoName [$($status.Branch)]"
            }
        }

        Write-Host  "$('>' * ($nestedPromptLevel + 1))" -NoNewline

        $global:LASTEXITCODE = $realLASTEXITCODE

        # Default console looks like this
        # PS C:\WINDOWS\system32>
        return ' '
    }
}

Install-PoshGit
Initialize-Git