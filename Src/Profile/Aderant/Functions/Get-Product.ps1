function global:Get-Product {
    <#
    .Synopsis
        Runs a GetProduct for the current branch
    .Description
        Uses the expertmanifest from the local Build.Infrastructure\Src\Package directory.
        This will always return the pdb's.
        The binaries will be loaded into your branch binaries directory. e.g. <your_branch_source>\Binaries
    .PARAMETER onlyUpdated
        Switch to indicate that only updated modules should get pulled in.
    .PARAMETER createBackup
        Switch to create a backup of the Binaries folder (named BinariesBackup in the same folder) after successfully retrieving the product.
        This is intended to be used by developers who call Copy-BinariesFromCurrentModules (cb) or Copy-BinToEnvironment and want to have a backup with the original files from the Get-Product call.
    .PARAMETER pullRequestId
        Mixes the output of a pull request build into the product
    .PARAMETER buildNumber
        Mixes the output of build into the product using buildNumber
    .EXAMPLE
        Get-Product -createBackup
    #>
    [CmdletBinding(DefaultParameterSetName='master')]
    param (
        [Parameter(Mandatory=$false, ParameterSetName = "Branch", Position = 0)][ValidateNotNullOrEmpty()][string]$branch,
        [Parameter(Mandatory=$false, ParameterSetName = "PullRequest")][Alias('pr')][ValidateNotNullOrEmpty()][int]$pullRequestId,
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][int]$buildNumber,
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$binariesDirectory,
        [string[]]$components = @('Product'),
        [switch]$createBackup,
        [switch]$buildNumberCheck
    )

    [string]$getProduct = Join-Path -Path $global:ShellContext.PackageScriptsDirectory -ChildPath 'Get-Product.ps1'
    [string]$dropRoot = '\\dfs.aderant.com\expert-ci'

    if ([string]::IsNullOrWhiteSpace($binariesDirectory)) {
        $binariesDirectory = "$Env:SystemDrive\AderantExpert\Binaries"
    }

    if ([string]::IsNullOrWhiteSpace($branch)) {
        $branch = $PSCmdlet.ParameterSetName
    }

    switch ($PSCmdlet.ParameterSetName) {
        'PullRequest' {
            & $getProduct -binariesDirectory $binariesDirectory -dropRoot $dropRoot -pullRequestId $pullRequestId -components $components -WhatIf:$($buildNumberCheck.IsPresent)
        }
        default {
            & $getProduct -binariesDirectory $binariesDirectory -dropRoot $dropRoot -branch $branch -buildNumber $buildNumber -components $components -WhatIf:$($buildNumberCheck.IsPresent)
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
    [Alias("gpb")]
    param(
        [switch]$copyToClipboard
    )

    $binariesDirectory = "$Env:SystemDrive\AderantExpert\Binaries"
    $buildVersionFile = Get-ChildItem -Path $binariesDirectory -Filter Expert_Build_*.url

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