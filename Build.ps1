function DownloadScript() 
{
    $response = wget "http://tfs:8080/tfs/aderant/expertsuite/_apis/git/repositories/build.infrastructure/items?api-version=1.0&scopePath=build.ps1" -UseDefaultCredentials

    return $response.RawContentStream
}

function Update() {
    Write-Host "Updating build system"	
    
    $stream = DownloadScript
    
    $newHash = Get-FileHash -InputStream $stream -Algorithm SHA256
    $thisFileHash = Get-FileHash -Path $PSCommandPath -Algorithm SHA256

    if ($thisFileHash.Hash -ne $newHash.Hash) {
        $stream.Position = 0

        $sr = new-object System.IO.StreamReader ($stream)    
        $script = $sr.ReadToEnd()

        if ($script) {
            Write-Host "Updating $PSCommandPath"
            $script | Out-File $PSCommandPath
        }
    }
}

function Build()
{
    if ($PSCommandPath -and (-not $Env:EXPERT_BUILD_UTIL_DIRECTORY -or $args[0] -eq "update")) {
        Update

        $newScriptBlock = Get-Content $PSCommandPath -Raw
        Invoke-Expression -Command "$newScriptBlock $args"
        return
    } else {
	    #& $Env:EXPERT_BUILD_UTIL_DIRECTORY\Build\BuildModule.ps1 $args
        Write-Host "Doing the build"
    }	
}

Build $args
