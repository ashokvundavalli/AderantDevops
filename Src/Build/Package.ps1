<#
#>
[CmdletBinding()]
param([string]$repository)

begin {    
    
}

process {        
    [Aderant.Build.Packaging.Packager]::Package($repository)
}

end {
  
}

