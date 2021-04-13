#Requires -RunAsAdministrator

function global:Enable-GitPrompt {
    Function global:Prompt {
        [int]$existingExitCode = $global:LASTEXITCODE

        Write-Host ([string]::Empty)
        Write-Host 'Module [' -NoNewline
        Write-Host ($global:ShellContext.CurrentModuleName) -NoNewline -ForegroundColor 'DarkCyan'
        Write-Host '] at [' -NoNewline
        Write-Host ($global:ShellContext.CurrentModulePath) -NoNewline -ForegroundColor 'DarkCyan'
        Write-Host ']'
        Write-Host "PS $((Get-Location).Path)" -NoNewline

        if ($global:ShellContext.PoshGitAvailable) {
            Write-VcsStatus
        }

        Write-Host  "$('>' * ($nestedPromptLevel + 1))" -NoNewline

        # Restore LASTEXITCODE to its original value.
        $global:LASTEXITCODE = $existingExitCode

        # Default console looks like this:
        # PS C:\WINDOWS\system32>
        return ' '
    }
}

Install-PoshGit -Version ([System.Version]::new(0, 7, 3))
Initialize-Git
Enable-GitPrompt