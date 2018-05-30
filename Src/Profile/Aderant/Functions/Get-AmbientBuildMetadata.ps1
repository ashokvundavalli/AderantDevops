function Get-AmbientBuildMetadata {
    <#
    .SYNOPSIS
    Gets metadata about the current build.
    
    .DESCRIPTION    
    The `Get-AmbientBuildMetadata` function gets information about the current build. It is exists to hide what CI flavour the current build is running under. 
    #>
    [CmdletBinding()]
    param(
    )

    Set-StrictMode -Version Latest

    function Get-EnvironmentVariable {
        param (
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty][string]$Name
        )

        Get-Item -Path ('env:{0}' -f $Name) -ErrorAction Ignore | Select-Object -ExpandProperty 'Value'
    }

    $buildInfo = [Aderant.Build.BuildMetadata]::new()

    if ((Test-Path -Path 'env:SYSTEM_TEAMFOUNDATIONCOLLECTIONURI')) {        
        $buildInfo.HostEnvironment = "vsts"

        $buildInfo.BuildNumber = Get-EnvironmentVariable 'BUILD_BUILDNUMBER'
        $buildInfo.BuildId = Get-EnvironmentVariable 'BUILD_BUILDID'
        $buildInfo.BuildUri = Get-EnvironmentVariable 'BUILD_BUILDURI'        
        
        $buildInfo.ScmUri = Get-EnvironmentVariable 'BUILD_REPOSITORY_URI'
        $buildInfo.ScmCommitId = Get-EnvironmentVariable 'GIT_COMMIT'
        $buildInfo.ScmBranch = Get-EnvironmentVariable 'BUILD_SOURCEBRANCH'
        $buildInfo.DebugLoggingEnabled = if (Get-EnvironmentVariable 'SYSTEM_DEBUG' -eq "true") { $true }

        $buildInfo.SetPullRequestInfo(
            (Get-EnvironmentVariable 'SYSTEM_PULLREQUEST_PULLREQUESTID'),
            (Get-EnvironmentVariable 'SYSTEM_PULLREQUEST_SOURCEBRANCH'),
            (Get-EnvironmentVariable 'SYSTEM_PULLREQUEST_TARGETBRANCH'))
    } else {        
        $buildInfo.HostEnvironment = "developer"
    }    

    return $buildInfo
}

Export-ModuleMember -function Get-AmbientBuildMetadata