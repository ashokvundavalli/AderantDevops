<#
    This file is intended for putting all the functions which are specific for web development.

	Note: This function is for debugging only. Not being called by the build tasks. Look at CompileLess.cs instead for live work.
#>

<# 
.Synopsis 
    Compile *.less files for the current module
.Description   
    Compile *.less into *.css for the current module
    All .less files in the Content directory that do not start with a '_' will be compiled into .css
.PARAMETER gwd
    Switch - boolean value to enable/disable whether to run a gwd in ExpressMode before start compiling the LESS file(s)
.EXAMPLE
    less
#>

function global:Compile-Less() {
    param ([switch] $gwd = $false)

    function CompileLess {
        if ($gwd) {
            Invoke-Expression "gwd -CompileLess"
        }

        $LessCompilerPath = Resolve-Path "$global:BuildScriptsDirectory\..\Build.Tools\LessCompiler\lessc.cmd"
        
        # iterate over any .less files in a Content directory and compile them into css
        $lessFiles = Get-ChildItem "$global:CurrentModulePath\Src\**\Content\*.less"
        if ($lessFiles -eq $null) {
            Write-Warning "No .less files found in module: $global:CurrentModuleName."
            return
        }
        foreach ($lessFile in $lessFiles) {
            if ($lessFile.Name.StartsWith("_")) {
                Write-Debug "Skipped compiling $lessFile.Name as it's filename begins with an '_'"
                continue
            }
            $cssFileName = $lessFile.Name.Replace($lessFile.Extension, ".css")
            $cssFile = "$($lessFile.Directory)\$($cssFileName)"

            Write-Host -ForegroundColor Yellow "Compiling less for: $lessFile"
			
            try {
                $exp = "$LessCompilerPath -ru $lessFile $cssFile --source-map"
                Invoke-expression $Exp
                if (!($lastExitCode) -eq 0) { 
                    Write-Host "LessCompiler error code $lastExitCode" -ForegroundColor red 
                }
            } catch {
                Write-Host "Invoke LessCompiler failed." -ForegroundColor red
            }
        }
    }

    # check if current module is null
    if ([string]::IsNullOrEmpty($global:CurrentModulePath) -and !$all) {    
        Write-Warning "No current module set."
    } else {
        CompileLess
    }
}


function global:Get-ChutzpahPath() {
    return Resolve-Path "$global:BuildScriptsDirectory\..\Build.Tools\Chutzpah\bin\chutzpah.console.exe"
}

function global:Run-Chutzpah() { 
    $ChutzpahPath = Get-ChutzpahPath
    & $ChutzpahPath $args
}
<# 
Runs chutzpah for chutzpah.json files
#>
function global:Run-WebTests() {
    param ([string] $ChutzpahJson, [switch] $coverage = $false, [switch] $openInBrowser = $false)

    function RunJsTests {
        $ChutzpahPath = Get-ChutzpahPath
        if ($ChutzpahJson) {
            $jsonFiles = $ChutzpahJson;
        } else {
            $jsonFiles = Get-ChildItem "$global:CurrentModulePath\**\*chutzpah.json" -recurse
        }
        # iterate over any chutzpah .json files in aany directory and run them.
        if ($jsonFiles -eq $null) {
            Write-Warning "No chutzpah.json files found in module: $global:CurrentModuleName."
            return
        }
        foreach ($jsonFile in $jsonFiles) {
            Write-Host -ForegroundColor Yellow "Runing tests for: $jsonFile"
			
            try {
                $exp = "$ChutzpahPath" + " $jsonFile" + " /showFailureReport"
                if ($coverage) {
                    $exp = $exp + " /coverage"
                }
                if ($openInBrowser) {
                    $exp = $exp + " /openInBrowser"
                }
                Write-Host "Running: "$exp
                Invoke-expression $Exp
                if (!($lastExitCode) -eq 0) { 
                    Write-Host "error code $lastExitCode" -ForegroundColor red 
                }
            } catch {
                Write-Host "Invoke Chutzpah failed." -ForegroundColor red
            }
        }
    }

    # check if current module is null
    if ([string]::IsNullOrEmpty($global:CurrentModulePath) -and !$all) {    
        Write-Warning "No current module set."
    } else {
        RunJsTests
    }
}

function SilentlyInstallNPM {
    Write-Warning "Please wait while Nodejs is being installed, this may take up to a minute..."
    cmd /c msiexec /i "http://nodejs.org/dist/v4.1.0/node-v4.1.0-x64.msi" /qn
    $title = "A restart of PowerShell is needed."
    $message = "Please re-open your PowerShell Window... "
    $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes", `
        "Yes"
    $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No", `
        "No"
    $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)
    $result = $host.ui.PromptForChoice($title, $message, $options, 0) 
    switch ($result) {
        0 {Exit}
        1 {Write-Warning "You selected No... Please restart PowerShell manually when you are ready."}
    }
}

<# 
.Synopsis 
    Changes current directory to the module root path and executes a gulp task. If no task is specified, the default task will be the default.
.PARAMETER ModuleName
    The name of the module to get dependencies from.
.PARAMETER CompileLess
    if specified will also compile the main less file.
.PARAMETER watch
    if specified will also watch for changes and copy them automatically.
.PARAMETER install
    if specified will also check for any missing node packages and install them (for gulp).
.EXAMPLE
        gwd Web.Presentation -CompileLess
    When run from Web.Case current module, will get all Web.Presentation dependencies and also compile the less.
#>
function global:Get-WebDependencies([string] $ModuleName, [switch] $CompileLess, [switch] $watch, [switch] $install) {

    #check that NodeJs is installed and if not, install it
    if (Get-Command npm -errorAction SilentlyContinue) {
        #Node.js and NPM exists, good to go
    } else {
        SilentlyInstallNPM
    }

    if (!($global:CurrentModuleName)) {
        Write-Host -ForegroundColor Red "There is no current module and you have not specified one.";
        Get-Help Get-WebDependencies
        return
    }

    if ($ModuleName -and !($ModuleName -eq "Web.SMB" -or $ModuleName -eq "Web.Presentation" -or $ModuleName -eq "Web.Foundation" -or $ModuleName -eq "Web.OTG")) {
        Write-Host -ForegroundColor Red "The Module"$ModuleName" is not supported."
        return
    }

    $OriginalModule = $global:CurrentModuleName
    $ModulesPath = $global:BranchModulesDirectory
	
    # run gulp task
    cd $GulpDirectory

    if ($install) {
        Write-Host -ForegroundColor Yellow "Installing gulp."
        npm install -g gulp
        npm install

        Set-CurrentModule $OriginalModule -quiet
        cd $global:CurrentModulePath
        return
    }

    #check that node_modules folder exists and if not, run npm install
    if ((Test-Path $GulpDirectory+"\node_modules") -ne $true) { 
        Write-Host -ForegroundColor Yellow "Installing NodeJs dependencies for gulp."
        npm install
    }

    if ($ModuleName) {
        if ($watch) {
            & gulp Watch$ModuleName --moduleName $OriginalModule --modulesPath $ModulesPath --silent
        }
        else {
            & gulp $ModuleName --moduleName $OriginalModule --modulesPath $ModulesPath --silent
        }
    }
    else {
        if ($watch) {
            & gulp WatchAll --moduleName $OriginalModule --modulesPath $ModulesPath --silent
        }
        else {
            & gulp --moduleName $OriginalModule --modulesPath $ModulesPath --silent
        }
    }

    Set-CurrentModule $OriginalModule -quiet
    cd $global:CurrentModulePath

    if ($CompileLess) {
        Write-Host -ForegroundColor Yellow "Compiling Less...";
        Compile-Less
    }
}
<#
.DESCRIPTION
Get Local Files From Web.Core

Example 
    cm C:\Source\Case
    gwd2 -Watch -CompileLess


Parameters

    Watch      ->   files will be automatically copied. 
                    Note: Once this is set the only way you can undo is by killing powershell

    CompileLess ->  .less files will be compiled after copying 
#>
function global:Get-WebDependencies2([switch] $Watch, [switch] $CompileLess) {
    $global:CompileLessAfterGwd2 = $CompileLess

    $action = {
        $webCore = "C:\Source\WebCore\Src\Web.Core";
        $destination = "C:\Source\$CurrentModuleName";
        
        if (Test-Path "$destination\Dependencies" -pathType container) {
            Copy-Item   $webCore\bin\Web.Core.dll               $destination\Dependencies                                           -Recurse -Force -Verbose
        }

        if (Test-Path "$destination\packages\Aderant.Web.Core\lib" -pathType container) {
            Copy-Item   $webCore\bin\Web.Core.dll               $destination\packages\Aderant.Web.Core\lib                                          -Recurse -Force -Verbose
            robocopy    $webCore\Content\Web.Presentation       $destination\packages\Aderant.Web.Core\lib\Web.Core\Content\Web.Presentation            *.less              /MIR /V
            robocopy    $webCore\ViewModels                     $destination\packages\Aderant.Web.Core\lib\Web.Core\ViewModels                          *.js *.d.ts         /MIR /V
            robocopy    $webCore\Scripts                        $destination\packages\Aderant.Web.Core\lib\Web.Core\Scripts                             *.js *.d.ts         /MIR /V /XD $webCore\Scripts\ThirdParty.KendoUI
        } 
        
        if (Test-Path "$destination\Dependencies\Web.Core" -pathType container) {
            robocopy    $webCore\Content\Web.Presentation       $destination\Dependencies\Web.Core\Content\Web.Presentation         *.less              /MIR /V
            robocopy    $webCore\ViewModels                     $destination\Dependencies\Web.Core\ViewModels                       *.js *.d.ts         /MIR /V
            robocopy    $webCore\Scripts                        $destination\Dependencies\Web.Core\Scripts                          *.js *.d.ts         /MIR /V /XD $webCore\Scripts\ThirdParty.KendoUI
        }

        # now copy things into proj/src/bin folders

        $webProjects = Get-ChildItem -path "$destination\Src" -Directory -filter "Web.*";

        foreach ($proj in $webProjects) {
            $projName = $proj.Name;

            if (Test-Path "$destination\Src\$projName\bin" -pathType container) {
                Copy-Item   $webCore\bin\Web.Core.dll           $destination\Src\$projName\bin                                          -Recurse -Force -Verbose
            }

            if (Test-Path "$destination\Src\$projName\Views\Shared\Web.Presentation" -pathType container) {
                robocopy    $webCore\Views\Shared\Web.Presentation  $destination\Src\$projName\Views\Shared\Web.Presentation             *html               /MIR /V
            }
        }

        if ($CompileLessAfterGwd2) {
            less
        }
    }

    & $action

    if ($watch) {
        watch "C:\Source\WebCore\Src\Web.Core" "*.*" $action
    }
}

function watch($folder, $filter, $action) {
    $Global:watchChangeTriggered = $false;
    $fsw = New-Object System.IO.FileSystemWatcher $folder, $filter -Property @{ 
        IncludeSubdirectories = $true; 
        NotifyFilter          = [IO.NotifyFilters]'FileName, LastWrite'
    } 
   
    # register event for file changes
    Register-ObjectEvent $fsw Changed -SourceIdentifier FileChanged -Action {
        $Global:watchChangeTriggered = $true;
    }

    while ($true) {
        if ($Global:watchChangeTriggered) {
            & $action
            $Global:watchChangeTriggered = $false;
        }
        Start-Sleep -s 10
    }
}

Export-ModuleMember -function Get-WebDependencies 
Export-ModuleMember -function Get-WebDependencies2
Export-ModuleMember -Function Compile-Less
Export-ModuleMember -Function Run-Chutzpah
Export-ModuleMember -Function Run-WebTests

Set-Alias gwd Get-WebDependencies -Scope Global
Set-Alias gwd2 Get-WebDependencies2 -Scope Global
Set-Alias less Compile-Less -Scope Global
Set-Alias rwb Run-WebTests -Scope Global
Set-Alias chutzpah Run-Chutzpah -Scope Global
