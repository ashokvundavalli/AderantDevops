[CmdletBinding()]
param(
    [string]$repository
)

begin {
    . $PSScriptRoot\Build-Libraries.ps1

    function PushPackage([string]$package) {
        if (-not $package) {
            throw "No package provided"
        }

        Invoke-Tool -FileName $paket -Arguments "push file $package url http://packages.ap.aderant.com/packages/ apikey `" `"" -RequireExitCodeZero | 
            ForEach-Object {
            $_
        }
    }
}

process {
    # TODO: Abstract to property
    $paket = [System.IO.Path]::Combine($Env:EXPERT_BUILD_DIRECTORY, "Build", "paket.exe")
    $gitVersion = [System.IO.Path]::Combine($Env:EXPERT_BUILD_DIRECTORY, "Build", "GitVersion.exe")

    Write-Host "Calculating version for: $repository"

    # Try to get version number from the current git branch infomation.
    try {
        $text = (& $gitVersion $repository /output json | Out-String )    

        $versionJson = ConvertFrom-Json $text
        $versionSem = $versionJson.FullSemVer

        if (-not $versionSem) {
            Write-Host "##vso[task.logissue type=error;] Unable to get gitversion. The last returned text is: $text"
        } else {
            Write-Host "Full version is $versionSem"
        }
    } catch {
        Write-Error "##vso[task.logissue type=error;] Failed to get correct version number from the git repository. Returned info from GitVersion:"
        Write-Error "$text"
        
        throw "Failed to generate a version for the repository. Inspect the logs for more detail."
    }
    
    Write-Host "Creating package version"

    # For NuGet sorting we need to remove all dashes from the package name, otherwise we can't have different release channels per branch
    # as the sorting algorithm will get confused
    $version = [Aderant.Build.Packaging.Packager]::CreatePackageVersion($text)

    Write-Host "Creating package"

    #Write-Host "Calling Cmdlet: Publish-ExpertPackage $repository $version" 
    $packResult = New-ExpertPackage $repository $version

    if ($packResult) {
        if (Test-Path $packResult.OutputPath) {
            if (($Env:BUILD_SOURCEBRANCHNAME -eq "master"  -or $Env:BUILD_SOURCEBRANCH -like "*releases/*") -and -not $global:IsDesktopBuild) {
                Write-Host "Pushing packages from: $($packResult.OutputPath)"        
            
                gci -Path $packResult.OutputPath -Filter *.nupkg | % { PushPackage $_.FullName }        

                $buildNumber = ("{0} {1}" -f $Env:BUILD_REPOSITORY_NAME, $versionSem)
                Write-Host "BuildNumber is [$buildNumber]"

                Write-Output ("##vso[task.setvariable variable=build.buildnumber;]" + $buildNumber)
                Write-Output ("##vso[build.updatebuildnumber]" + $buildNumber)
            }   
        }    
    } else {
        Write-Host "Packaging skipped"
    }    
}

end {
  
}

