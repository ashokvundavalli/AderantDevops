function global:Initialize-Git
{
    <#
    .SYNOPSIS
        Configures the Git SCM tool to work with TFS and installs some useful global aliases for developers
    #>
    [CmdletBinding()]
    param(
        [Aderant.Build.BuildOperationContext]
        $Context = (Get-BuildContext -CreateIfNeeded)
    )

    Set-StrictMode -Version 'Latest'
    Use-CallerPreference -Cmdlet $PSCmdlet -SessionState $ExecutionContext.SessionState

    ConfigureGit $Context
}

function CreateDummyGitDirectory([string]$directory) {
    # Will cause Git to not remove this directory unless you are in the directory itself
    if (Test-Path ($directory)) {
        if (-not (Test-Path ("$directory\.git"))) {
            & git init $directory
        }
    }
}

<#
.SYNOPSIS
    Ensures that 'git clean' always preserves the SharedBin
#>
function AddGitCommandIntercept() {
    $ExecutionContext.SessionState.InvokeCommand.PreCommandLookupAction = {
        param($command, $eventArgs)

        # not executed internally by PowerShell
        if ($command -eq 'git' -and $eventArgs.CommandOrigin -eq 'Runspace') {
            # tell PowerShell what to do instead of
            # running the original command
            $eventArgs.CommandScriptBlock = {

            $(
                $newArgs = $args

                $dirsToKeep  = @("SharedBin", ".vscode", ".vs")

                if ($newArgs.Count -eq 0) {
                  & $command
                } else {
                  foreach ($arg in $args) {
                    if ($arg -eq "clean") {
                      foreach ($dirToKeep in $dirsToKeep) {
                        $newArgs += "-e"
                        $newArgs += $dirToKeep
                      }
                    }
                  }
                  & $command $newArgs
                  }
                )
            }.GetNewClosure()
        }
    }
}

function ConfigureGit([Aderant.Build.BuildOperationContext]$context)
{
    Write-Debug "Configuring .gitconfig"

    try {
        $result = [bool]::Parse((& git config --get core.autocrlf))
        if ($result) {
            Write-Host (New-Object string -ArgumentList '*', 80) -ForegroundColor Red
            Write-Host "Your git config has autocrlf=true which will cause untold chaos." -ForegroundColor Red
            Write-Host "It will be changed to false." -ForegroundColor Red
            Write-Host (New-Object string -ArgumentList '*', 80) -ForegroundColor Red
            sleep -Seconds 10
        }
    } finally {
        # Probably don't have git so we are going to fail hard very soon.
    }

    & git config --global difftool.prompt false
    & git config --global credential.tfs.integrated true
    & git config --global credential.tfs.ap.aderant.com.integrated true
    & git config --global core.autocrlf false
    & git config --global http.emptyAuth true
    & git config --global credential.authority ntlm
    & git config --global fetch.prune true

    if ($context.IsDesktopBuild) {
        Write-Debug "Applying desktop .gitconfig"

        #& git config --global core.excludesfile "$buildScriptsDirectory\..\..\.gitignore"

        # Global Aliases - Insert nifty git commands here

        # Prints a list of branches you've committed to sorted by date
        & git config --global alias.branchdates "for-each-ref --sort=committerdate refs/heads/ --format='%(committerdate:short) %(refname:short)'"

        # Deletes all untracked files without wiping out any SharedBin symlinks
        & git config --global alias.scrub "clean -fdx -e SharedBin -e .vscode/"

        # Undoes the last commit
        & git config --global alias.undo-commit "reset --soft HEAD^"

        # Delete all local branches but master and the current one, only if they are fully merged with master.
        & git config --global alias.br-delete-useless "!f(){ git branch | grep -v 'master' | grep -v ^* | xargs git branch -d; }; f"

        # set up notepad++ as the default commit editor
        # & git config --global core.editor "'C:/Program Files (x86)/Notepad++/notepad++.exe' -multiInst -notabbar -nosession -noPlugin"
    }
}