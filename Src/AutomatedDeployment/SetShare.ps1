process{

	if  (-Not (Test-Path C:\ExpertShare)) {
		Write-Verbose "Creating Expert Share" -Verbose
		New-Item C:\ExpertShare -ItemType Directory
	} else {
		Write-Verbose "Expert Share already Exists" -Verbose
	}

	if (-Not(Get-WmiObject Win32_Share -Filter "path='C:\\ExpertShare'")){

		Write-Verbose "Sharing Expert Share" -Verbose

		$shareObject = (Get-WmiObject Win32_share -List).Create("C:\ExpertShare","ExpertShare",0)
    
        Grant-SmbShareAccess -Name ExpertShare -AccountName Everyone -AccessRight Full -Force

		Exit ($shareObject.ReturnValue)

	} else {
		Write-Verbose "Expert share already shared"
		Exit 0
	}

}