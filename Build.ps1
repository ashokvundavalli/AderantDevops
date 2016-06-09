param(
	[ValidateSet('build','update')]
	[string]$action
)

function DownloadScript() 
{
    $response = wget "http://tfs:8080/tfs/aderant/expertsuite/_apis/git/repositories/build.infrastructure/items?api-version=1.0&scopePath=build.ps1" -UseDefaultCredentials

    return $response.RawContentStream
}

function Update() 
{
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

function Build([string]$action)
{    
    $updateRequested = $action -eq "update"

    if ($PSCommandPath -and (-not $Env:EXPERT_BUILD_UTIL_DIRECTORY -or $updateRequested)) {
        Update
		
		if (-not $Env:EXPERT_BUILD_UTIL_DIRECTORY) {
			$Env:EXPERT_BUILD_UTIL_DIRECTORY = [System.IO.Path]::Combine($PSCommandPath, ".buildutils")
		}
        
        if (-not $updateRequested) {
            & $PSCommandPath -action build
        }
    } else {
	    #& $Env:EXPERT_BUILD_UTIL_DIRECTORY\Build\BuildModule.ps1 $args
        Write-Host "Doing the build: $PSScriptRoot"
    }	
}

Build $action

