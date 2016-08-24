<#
    This file is intended for putting all the functions which are specific for web development.
#>

<# 
.Synopsis 
    Compile Aderant.*.less files for the current module and put them into pending changes
.Description   
    Compile Aderant.*.less for Aderant.*.css and Aderant.*.css.map (if it is a SMB module) for the current module
    Involves changes in file types of .LESS in Web Projects
.PARAMETER gwd
    Switch - boolean value to enable/disable whether to run a gwd in ExpressMode before start compiling the LESS file(s)
.PARAMETER install
    Switch - boolean value to enable/disable whether to run the installation for the prerequisites first, this includes Node.js NPM and LessCss compiler
.EXAMPLE
    css
#>

function global:Compile-LessFilesForWebProjects() {
    param ([switch] $gwd = $false, [switch] $install = $false)

    function CompileLess {
        if ($gwd) {
            Invoke-Expression "gwd -CompileLess"
        }

        # Part 1: set up root directories
        $CurrentModule = $global:CurrentModuleName
        $CurrentWebProject = $CurrentModule.split('.')[1]
        $ContentDirectoryFullPath = "$global:BranchModulesDirectory\$CurrentModule\Src\$CurrentModule\Content"
        
        if ($CurrentWebProject.StartsWith("Case", "CurrentCultureIgnoreCase") -or 
            $CurrentWebProject.StartsWith("Administration", "CurrentCultureIgnoreCase") -or 
            $CurrentWebProject.StartsWith("Financials", "CurrentCultureIgnoreCase") -or 
            $CurrentWebProject.StartsWith("Test", "CurrentCultureIgnoreCase")) {

            # iterate over skin less files and compile them into css (checking out from tfs)
            $lessFiles = Get-ChildItem $ContentDirectoryFullPath\Skin.*.less
            foreach ($lessFile in $lessFiles) {
                $cssFileName = $lessFile.Name.Substring(0, $lessFile.Name.Length-4) + "css"
                $cssFile = "$($lessFile.Directory)\$($cssFileName)"

                Write-Host -ForegroundColor Yellow "Checking out: $cssFile"
                Invoke-Expression "tf checkout $cssFile"

                Write-Host -ForegroundColor Yellow "Compiling less: $lessFile"
                Invoke-Expression "lessc -ru $lessFile > $cssFile"
            }
        }
        else {
            $AderantLess = "$ContentDirectoryFullPath\Aderant.$CurrentWebProject.less"
            $AderantCss = "$ContentDirectoryFullPath\Aderant.$CurrentWebProject.css"

            Write-Host -ForegroundColor Yellow "Checking out: $AderantCss"
            Invoke-Expression "tf checkout $AderantCss"

            Write-Host -ForegroundColor Yellow "Compiling less: $AderantLess"
            Invoke-Expression "lessc -ru $AderantLess > $AderantCss"
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
    
    function SilentlyInstallLESSC {
        npm install -g less
    }
    
    if (Get-Command npm -errorAction SilentlyContinue) {
        #Write-Host "Node.js and NPM exists, good to go!"
    } else {
        SilentlyInstallNPM
    }
    
    if (Get-Command lessc -errorAction SilentlyContinue) {
        #Write-Host "LESS Compiler lessc exists, good to go!"
    } else {
        SilentlyInstallLESSC
    }

    if($install) {
        break;
    }

    # check if current module is null
    if ([string]::IsNullOrEmpty($global:CurrentModulePath) -and !$all) {    
        Write-Warning "The current module is not set so the binaries will not be copied"
    } else {
        # check if current module is a web module and is not Web.Foundation
        if($global:CurrentModuleName.StartsWith("Web", "CurrentCultureIgnoreCase") -and !$global:CurrentModuleName.EndsWith("Foundation", "CurrentCultureIgnoreCase")  -and !$global:CurrentModuleName.EndsWith("Presentation", "CurrentCultureIgnoreCase")){
            CompileLess
        } else {
            Write-Warning "Only web modules need this to be completed."
        }
    }
}

Set-Alias css Compile-LessFilesForWebProjects
Export-ModuleMember -function Compile-LessFilesForWebProjects -alias css

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

    if ($ModuleName -and !($ModuleName -eq "Web.SMB" -or $ModuleName -eq "Web.Presentation" -or $ModuleName -eq "Web.Foundation")) {
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
    if((Test-Path $GulpDirectory+"\node_modules") -ne $true){ 
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
        Compile-LessFilesForWebProjects
    }
}

Export-ModuleMember -function Get-WebDependencies 
Export-ModuleMember -Function Compile-LessFilesForWebProjects

Set-Alias gwd Get-WebDependencies -Scope Global
Set-Alias less Compile-LessFilesForWebProjects -Scope Global