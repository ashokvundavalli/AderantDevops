$currentUser = [Security.Principal.WindowsPrincipal]([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdminProcess = $currentUser.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$adminHeader = if ($isAdminProcess) { 'Administrator: ' } else { '' }

function global:Enable-GitPrompt{
    Function global:prompt {
        $realLASTEXITCODE = $LASTEXITCODE    
    
        $location = Get-Location
    
        Write-Host("")
        Write-Host ("Module [") -NoNewline
        Write-Host ($global:CurrentModuleName) -NoNewline -ForegroundColor DarkCyan
        Write-Host ("] at [") -NoNewline
        Write-Host ($global:CurrentModulePath) -NoNewline -ForegroundColor DarkCyan
        Write-Host ("]")
    
        Write-Host "PS $(location)" -NoNewline
    
        if ($ShellContext.PoshGitAvailable) {
            Write-VcsStatus
    
            $status = Get-GitStatus
    
            if ($status -ne $null) {    
                $repoName = Split-Path -Leaf (Split-Path $status.GitDir)    
                $Host.UI.RawUI.WindowTitle = "$script:adminHeader$repoName [$($status.Branch)]"
            }
        }
    
        Write-Host  "$('>' * ($nestedPromptLevel + 1))" -NoNewline
       
        $global:LASTEXITCODE = $realLASTEXITCODE
           
        # Default console looks like this
        # PS C:\WINDOWS\system32> 
        return " "
    }
}

. $PSScriptRoot\Functions\InvokeBuild.ps1

Install-PoshGit
Initialize-Git

Export-ModuleMember -Function Invoke-Build
Set-Alias -Name bm -Value Invoke-Build -Scope Global