function DownloadScript() 
{
    $response = wget "http://tfs:8080/tfs/aderant/expertsuite/_apis/git/repositories/build.infrastructure/items?api-version=1.0&scopePath=build.ps1" -UseDefaultCredentials
    return $response
}

function Build()
{
    if (-not $Env:EXPERT_BUILD_UTIL_DIRECTORY -or $args.Contains("update")) {
	    Write-Host "Updating build system"	
    
	    $response = DownloadScript
    
        $newHash = Get-FileHash -InputStream $result.RawContentStream -Algorithm SHA256
	    $thisFileHash = Get-FileHash -Path $PSCommandPath -Algorithm SHA256

        if ($thisFileHash -ne $newHash) {
            $sr = new-object System.IO.StreamReader ($response.RawContentStream)    
            $script = $sr.ReadToEnd() | Out-File "foo.txt"
	    }
    } else {
	    & $Env:EXPERT_BUILD_UTIL_DIRECTORY\Build\BuildModule.ps1 $args
    }
}