param([string]$environmentVariableName)

if(Test-Path Env:\$environmentVariableName){
	write "removed Env:\$environmentVariableName"	
   	Remove-Item Env:\$environmentVariableName
}else{
	write "Env:\$environmentVariableName was not set so could not be removed!"
}