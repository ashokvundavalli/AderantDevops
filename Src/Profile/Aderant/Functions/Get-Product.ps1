function global:Get-Product {
    <#
    .SYNOPSIS
        Retrieves binaries from the drop location.
    .DESCRIPTION
        Retrieves binaries from the drop location and unzips them to the binaries directory.
    .PARAMETER Branch
        The branch to retrieve build artifacts for. Defaults to 'master'.
    .PARAMETER PullRequestId
        [ALIAS]: pr
        The pull request id to retrieve build artifacts for.
    .PARAMETER BuildNumber
        [ALIAS]: Build, bn
        The build id to retrieve build artifacts for.
    .PARAMETER BinariesDirectory
        The location to unzip the retrieved binaries to. By default this is: C:\AderantExpert\Binaries.
    .PARAMETER Components
        The list of components to retrieve from the drop location. Available options include: 'product', 'packages' and 'update'.
    .PARAMETER CreateBackup
        Switch to create a backup of the Binaries folder (named BinariesBackup in the same folder) after successfully retrieving the product.
        This is intended to be used by developers who call Copy-BinariesFromCurrentModules (cb) or Copy-BinToEnvironment and want to have a backup with the original files from the Get-Product call.
    .PARAMETER BuildNumberCheck
        Retrives the latest build number for the specified branch.
    .EXAMPLE
        
    Get-Product

    Parameterless calls to `Get-Product` retrieve the most recent successful build of master.

    .EXAMPLE

    Get-Product -Branch MyBranch

    Downloads the binaries associated with the given branch.

    .EXAMPLE

    Get-Product -PullRequestId 19159
    - OR -

    PS C:\>Get-Product -pr 19159

    Downloads the binaries associated with the given pull request.

    .EXAMPLE

    Get-Product -BuildNumber 123456
    - OR -

    PS C:\>Get-Product -Build 123456

    - OR -

    PS C:\>Get-Product -bn 123456

    Downloads the binaries associated with the given build number.

    .EXAMPLE

    Get-Product -BuildNumberCheck

    Running 'Get-Product' with the following parameters:
    Name                           Value
    ----                           -----
    BinariesDirectory              C:\AderantExpert\Binaries
    DropRoot                       \\dfs.aderant.com\expert-ci
    Branch                         master
    PullRequestId                  0
    BuildNumber                    0
    Components                     {product}



    What if: Performing the operation "check build number" on target "master".
    The latest successful Build Number for branch: master is: 1149793.
    1149793

    PS C:\>Get-Product -branch update/82SP2ex0006 -BuildNumberCheck

    Name                           Value
    ----                           -----
    BinariesDirectory              C:\AderantExpert\Binaries
    DropRoot                       \\dfs.aderant.com\expert-ci
    Branch                         update/82SP2ex0006
    PullRequestId                  0
    BuildNumber                    0
    Components                     {product}



    What if: Performing the operation "check build number" on target "update/82SP2ex0006".
    The latest successful Build Number for branch: update/82SP2ex0006 is: 1149596.
    1149596

    The first call is retrieving the last successful build id on the master branch. The second call is retrieving the last succesful build id on a specified branch.
    #>
    [CmdletBinding(DefaultParameterSetName='Branch')]
    param (
        [Parameter(Mandatory=$false, ParameterSetName = 'Branch', Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Branch = 'master',

        [Parameter(Mandatory=$false, ParameterSetName = 'PullRequest')]
        [Alias('pr')]
        [int]$PullRequestId,

        [Parameter(Mandatory=$false, ParameterSetName = 'BuildNumber')]
        [Alias('Build', 'bn')]
        [int]$BuildNumber,

        [Parameter(Mandatory=$false)]
        [ValidateNotNullOrEmpty()]
        [string]$binariesDirectory = "$Env:SystemDrive\AderantExpert\Binaries",

        [Parameter(Mandatory=$false)]
        [ValidateNotNullOrEmpty()]
        [ValidateSet('product', 'packages', 'update')]
        [string[]]$Components = @('product'),

        [switch]$CreateBackup,

        [switch]$BuildNumberCheck
    )

    begin {
        [string]$getProduct = Join-Path -Path $global:ShellContext.PackageScriptsDirectory -ChildPath 'Get-Product.ps1'
        [string]$dropRoot = '\\dfs.aderant.com\expert-ci'
    }

    process {
        switch ($PSCmdlet.ParameterSetName) {
            'Branch' {
                & $getProduct -BinariesDirectory $binariesDirectory -DropRoot $dropRoot -Branch $branch -Components $components -WhatIf:$($buildNumberCheck.IsPresent)
                break
            }
            'PullRequest' {
                & $getProduct -BinariesDirectory $binariesDirectory -DropRoot $dropRoot -PullRequestId $pullRequestId -Components $components -WhatIf:$($buildNumberCheck.IsPresent)
                break
            }
            'BuildNumber' {
                & $getProduct -BinariesDirectory $binariesDirectory -DropRoot $dropRoot -BuildNumber $buildNumber -Components $components -WhatIf:$($buildNumberCheck.IsPresent)
                break
            }
        }

        if (-not $buildNumberCheck.IsPresent -and $createBackup.IsPresent) {
            Write-Host "Creating backup of Binaries folder."
            $backupPath = $global:ShellContext.BranchLocalDirectory + "\BinariesBackup"
            if (-not (Test-Path $backupPath)) {
                New-Item -ItemType Directory -Path $backupPath
            }
            Invoke-Expression "robocopy.exe $binariesDirectory $backupPath /MIR /SEC /TEE /R:2 /XD $binariesDirectory\ExpertSource\Customization" | Out-Null
            Write-Host "Backup complete."
        }
    }
}

#TODO: Front end with the http build service to cache the results for remote clients
Register-ArgumentCompleter -CommandName Get-Product -ParameterName "pullRequestId" -ScriptBlock {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $boundParameters)

    # TODO: Externalize
    # TODO: Call build service for caching for people in the US
    $stem = 'https://tfs.ap.aderant.com/tfs/ADERANT/ExpertSuite'
    $results = Invoke-RestMethod -Uri "$stem/_apis/git/pullrequests" -ContentType "application/json" -UseDefaultCredentials

    $ids = $results.value | Select-Object -Property pullRequestId, title

    if (-not $wordToComplete.EndsWith("*")) {
        $wordToComplete += "*"
    }

    $ids | Where-Object -FilterScript { $_.pullRequestId -like $wordToComplete -or $_.title -like $wordToComplete } | ForEach-Object {
        [System.Management.Automation.CompletionResult]::new($_.pullRequestId, $_.title, [System.Management.Automation.CompletionResultType]::Text, $_.title)
    }
}

function global:Get-ProductBuild {
    <#
    .Synopsis
        Displays the Build version url located in the binaries directory of the current branch.
    .Description
        WARNING: If you have done a Get-Product since your last deployment, then it will show the version number of the Get-Product rather than what is deployed.
    .PARAMETER copyToClipboard
        If specified the Build version will be copied to the clipboard.
    #>
    [CmdletBinding()]
    [Alias('gpb')]
    param(
        [switch]$copyToClipboard
    )

    $binariesDirectory = "$Env:SystemDrive\AderantExpert\Binaries"
    $buildVersionFile = Get-ChildItem -Path $binariesDirectory -Filter 'Expert_Build_*.url'

    if ($buildVersionFile) {
        $buildVersionFilePath = Join-Path -Path $binariesDirectory -ChildPath $buildVersionFile
        Invoke-Item $buildVersionFilePath
        Write-Host "Current Build information is visible at: $($buildVersionFilePath)"

        if ($copyToClipboard) {
            Add-Type -AssemblyName "System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
            [System.Windows.Forms.Clipboard]::SetText($buildVersionFile)
        }
    } else {
        Write-Error "No url containing build information is present in: $($binariesDirectory) "
    }
}