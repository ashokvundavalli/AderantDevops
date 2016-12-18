<#
    This file is intended for putting all the functions which are specific for web development.
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

function global:Compile-LessFilesForWebProjects() {
    param ([switch] $gwd = $false)

    function CompileLess {
        if ($gwd) {
            Invoke-Expression "gwd -CompileLess"
        }

		$LessCompilerPath = Resolve-Path "$global:BuildScriptsDirectory\..\Build.Tools\LessCompiler\lessc.cmd"
        
		# iterate over any .less files in a Content directory and compile them into css
		$lessFiles = Get-ChildItem "$global:CurrentModulePath\Src\**\Content\*.less"
		if($lessFiles -eq $null){
			Write-Warning "No .less files found in module: $global:CurrentModuleName."
			return
		}
		foreach ($lessFile in $lessFiles) {
			if($lessFile.Name.StartsWith("_")) {
				Write-Debug "Skipped compiling $lessFile.Name as it's filename begins with an '_'"
				continue
			}
			$cssFileName = $lessFile.Name.Replace($lessFile.Extension, ".css")
			$cssFile = "$($lessFile.Directory)\$($cssFileName)"

			Write-Host -ForegroundColor Yellow "Compiling less for: $lessFile"
			Invoke-Expression "$LessCompilerPath -ru $lessFile $cssFile"
		}
    }

    # check if current module is null
    if ([string]::IsNullOrEmpty($global:CurrentModulePath) -and !$all) {    
        Write-Warning "No current module set."
    } else {
        CompileLess
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