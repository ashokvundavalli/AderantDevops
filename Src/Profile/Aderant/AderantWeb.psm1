<#
	This file is intended for putting all the functions which are specific for web development.
#>

function tryToRemove ($path){
	Try {
		"Removing $path";
		Remove-Item $path -recurse -Force;
	} Catch
	   {
		[system.exception]
		"caught a system exception"
	}
}

<# 
.Synopsis 
	Cleans the files from the web modules which are not in the source control.
.Description
	Following files will be deleted in the module:
		Dependencies\* 
		Bin\*
		Src\$ModuleName\bin\*
	Also other files could be removed if scorch flag is on.
.PARAMETER ModuleNames
	An array of module names for which you want to clean.
.PARAMETER Scorch
	Use this switch if you want to do a scorch as well.
.EXAMPLE
	Clean Web.Presentation, Web.Foundation -Scorch
#>
function clean($ModuleNames= $global:CurrentModuleName, [switch] $Scorch) {
	foreach($moduleName in $ModuleNames){
		$path = Join-Path $global:BranchLocalDirectory "Modules\$ModuleName";

		tryToRemove $path\Dependencies\*
		tryToRemove $path\Bin\*
		tryToRemove $path\Src\$ModuleName\bin\*        
	}
	if($Scorch){
		Scorch $ModuleNames;
	}
}
Export-ModuleMember -function clean;
Add-ModuleExpansionParameter –CommandName "clean" –ParameterName "ModuleNames" -IsDefault

<# 
.Synopsis 
	Scorch the given web modules.  
.PARAMETER ModuleNames
	An array of module names for which you want to scorch.
.EXAMPLE
	Scorch Web.Presentation, Web.Foundation
#>
function scorch($ModuleNames= $global:CurrentModuleName) {
	foreach($moduleName in $ModuleNames) {
		$path = Join-Path $global:BranchLocalDirectory "Modules\$ModuleName";
		invoke-expression "tfpt scorch $path /recursive /noprompt";       
	}
}
Export-ModuleMember -function scorch;
Add-ModuleExpansionParameter –CommandName "scorch" –ParameterName "ModuleNames" -IsDefault





