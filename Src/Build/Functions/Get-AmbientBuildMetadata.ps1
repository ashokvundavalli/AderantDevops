<#
.SYNOPSIS
    Gets metadata about the current build.
.DESCRIPTION
    The `Get-AmbientBuildMetadata` function gets information about the current build.
    It is exists to hide what CI flavour the current build is running under.
#>
function Get-AmbientBuildMetadata {
    [CmdletBinding()]
    [OutputType([Aderant.Build.BuildMetadata])]
    param(
    )

    begin {
        Set-StrictMode -Version Latest

        function Get-EnvironmentVariable {
            param (
                [string]$Name
            )

            Get-Item -Path ('env:{0}' -f $Name) -ErrorAction Ignore | Select-Object -ExpandProperty 'Value'
        }
    }

    process {
        $buildInfo = [Aderant.Build.BuildMetadata]::new()

        if ((Test-Path -Path 'env:SYSTEM_TEAMFOUNDATIONCOLLECTIONURI')) {
            $buildInfo.BuildNumber = Get-EnvironmentVariable 'BUILD_BUILDNUMBER'
            $buildInfo.BuildId = Get-EnvironmentVariable 'BUILD_BUILDID'
            $buildInfo.BuildUri = Get-EnvironmentVariable 'BUILD_BUILDURI'
            $buildInfo.Flavor = Get-EnvironmentVariable 'BUILD_FLAVOR'

            $buildInfo.ScmUri = Get-EnvironmentVariable 'BUILD_REPOSITORY_URI'
            $buildInfo.ScmCommitId = Get-EnvironmentVariable 'BUILD_SOURCEVERSION'
            $buildInfo.ScmBranch = Get-EnvironmentVariable 'BUILD_SOURCEBRANCH'
            $buildInfo.BuildSourcesDirectory = Get-EnvironmentVariable 'BUILD_SOURCESDIRECTORY'

            if (Get-EnvironmentVariable 'SYSTEM_DEBUG' -eq "true") { $buildInfo.DebugLoggingEnabled = $true }

            if (Get-EnvironmentVariable 'BUILD_REASON' -eq "PullRequest") {
                $buildInfo.SetPullRequestInfo(
                    (Get-EnvironmentVariable 'SYSTEM_PULLREQUEST_PULLREQUESTID'),
                    (Get-EnvironmentVariable 'SYSTEM_PULLREQUEST_SOURCEBRANCH'),
                    (Get-EnvironmentVariable 'SYSTEM_PULLREQUEST_TARGETBRANCH'))
                }
        }

        return $buildInfo
    }
}