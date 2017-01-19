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

	# This step is to fix the server build bug with GitVersion on TFS 2017 by force fetching additional heads.
	try {
		if (-not $IsDesktopBuild) {
			Write-Host "Fetching all heads to ensure GitVersion.exe can get correct version number..."

			if (-not $env:SYSTEM_ACCESSTOKEN){
				Write-Host "##vso[task.logissue type=error;BuildDetail.Status = BuildStatus.Failed;] SYSTEM_ACCESSTOKEN is empty. Please enable `"Allow Scripts to Access OAuth Token`" in the Options tab of the build definition. This is a new requirement to do once per solution."
			} else {
				$pathToGit = "C:\Program Files\Git\cmd\git.exe"  # hard coded as the server normally do not change this address
				& $pathToGit  -c http.extraheader="AUTHORIZATION: bearer $env:SYSTEM_ACCESSTOKEN" fetch --tags --prune --progress origin
			}
		}
	} catch {
		Write-Error "##vso[task.logissue type=warning;] Failed to execute this command: [$pathToGit] which is abnormal."
		Exit 1
	}

	# Try to get version number from the current git branch infomation.
	try {
		$text = (& $gitVersion $repository /output json | Out-String )    

		$versionJson = ConvertFrom-Json $text
		$version = $versionJson.FullSemVer
		Write-Host "Full version is $version"
	} catch {
		Write-Error "##vso[task.logissue type=error;] Failed to get correct version number from the git repository. Returned info from GitVersion:"
		Write-Error "$text"
		exit 1
	}

    # For NuGet sorting we need to remove all dashes from the package name, otherwise we can't have different release channels per branch
    # as the sorting algorithm will get confused
    $version = [Aderant.Build.Packaging.Packager]::CreatePackageVersion($text)

	#Write-Host "Calling Cmdlet: Publish-ExpertPackage $repository $version" 
	$packResult = New-ExpertPackage $repository $version


    if ($packResult) {
        if (Test-Path $packResult.OutputPath) {
            if (($Env:BUILD_SOURCEBRANCHNAME -eq "master"  -or $Env:BUILD_SOURCEBRANCH -like "*releases/*") -and -not $global:IsDesktopBuild) {
                Write-Host "Pushing packages from: $($packResult.OutputPath)"        
            
                gci -Path $packResult.OutputPath -Filter *.nupkg | % { PushPackage $_.FullName }        

                $buildNumber = ("{0} {1}" -f $Env:BUILD_REPOSITORY_NAME, $version)
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

