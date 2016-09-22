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
    $text = (& $gitVersion $repository /output json | Out-String )    

    $versionJson = ConvertFrom-Json $text
    
    # For NuGet sorting we need to remove all dashes from the package name, otherwise we can't have different release channels per branch
    # as the sorting algorithm will get confused
    $version = [Aderant.Build.Packaging.Packager]::CreatePackageVersion($text)

	#Write-Host "Calling Cmdlet: Publish-ExpertPackage $repository $version" 
	$packResult = New-ExpertPackage $repository $version


    if ($packResult) {
        if (Test-Path $packResult.OutputPath) {
            if ($Env:BUILD_SOURCEBRANCHNAME -eq "master" -and -not $global:IsDesktopBuild) {
                Write-Host "Pushing packages from: $($packResult.OutputPath)"        
            
                gci -Path $packResult.OutputPath -Filter *.nupkg | % { PushPackage $_.FullName }        

                $buildNumber = ("{0} {1}" -f $Env:BUILD_REPOSITORY_NAME, $versionJson.FullSemVer)

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

