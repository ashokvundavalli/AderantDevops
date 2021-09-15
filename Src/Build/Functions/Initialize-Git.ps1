function global:Initialize-Git {
    <#
    .SYNOPSIS
        Configures the Git SCM tool to work with TFS and installs some useful global aliases for developers.
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

function Add-GitCommandIntercept {
    <#
    .SYNOPSIS
        Ensures that 'git clean' always preserves the SharedBin.
    #>

    $ExecutionContext.SessionState.InvokeCommand.PreCommandLookupAction = {
        param(
            $command,
            $eventArgs
        )

        # not executed internally by PowerShell
        if ($eventArgs.CommandOrigin -eq [System.Management.Automation.CommandOrigin]::Runspace) {
            if ($command -ne 'git') {
                return
            }
            # tell PowerShell what to do instead of
            # running the original command
            $eventArgs.CommandScriptBlock = {
                $newArgs = $args

                [string[]]$dirsToKeep  = @('SharedBin', '.vscode', '.vs')

                if ($newArgs.Count -eq 0) {
                  & $command
                } else {
                    foreach ($arg in $args) {
                        if ($arg -eq 'clean') {
                            foreach ($dirToKeep in $dirsToKeep) {
                                $newArgs += '-e'
                                $newArgs += $dirToKeep
                            }
                            break
                        }
                    }

                    & $command $newArgs
                }
            }.GetNewClosure()
        }
    }
}

function ConfigureGit([Aderant.Build.BuildOperationContext]$context) {
    Write-Debug "Configuring .gitconfig"

    try {
        $result = [bool]::Parse((& git config --get core.autocrlf))
        if ($result) {
            Write-Host (New-Object string -ArgumentList '*', 80) -ForegroundColor Red
            Write-Host "Your git config has autocrlf=true which will cause untold chaos." -ForegroundColor Red
            Write-Host "It will be changed to false." -ForegroundColor Red
            Write-Host (New-Object string -ArgumentList '*', 80) -ForegroundColor Red
            Start-Sleep -Seconds 10
        }
    } finally {
        # Probably don't have git so we are going to fail hard very soon.
    }

    # See https://git-scm.com/docs/git-config for official documentation on Git configuration.

    & git config --global difftool.prompt false
    & git config --global credential.tfs.integrated true
    & git config --global credential.tfs.ap.aderant.com.integrated true
    & git config --global core.autocrlf false
    & git config --global http.emptyAuth true
    & git config --global fetch.prune true

    if ($context.IsDesktopBuild) {
        Write-Debug 'Applying desktop .gitconfig'

        # Enable git push -u to push the current branch to update a branch with the same name on origin.
        & git config --global push.default current

        # Global Aliases - Insert nifty git commands here
        # Prints a list of branches you've committed to sorted by date
        & git config --global alias.branchdates "for-each-ref --sort=committerdate refs/heads/ --format='%(committerdate:short) %(refname:short)'"

        # Deletes all untracked files without wiping out any SharedBin symlinks
        & git config --global alias.scrub 'clean -fdx -e SharedBin -e .vscode/ -e .vs/'

        # Undoes the last commit
        & git config --global alias.undo-commit 'reset --soft HEAD^'

        # Delete all local branches but master and the current one, only if they are fully merged with master.
        & git config --global alias.br-delete-useless "!f(){ git branch | grep -v 'master' | grep -v ^* | xargs git branch -d; }; f"
    }
}
