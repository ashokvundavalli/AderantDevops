[CmdletBinding()]
Param ([string]$path)
         
Process {
    # Symbolic link needs to be removed using [System.IO.Directory]::Delete
    dir -Path $path -Force -Recurse -ErrorAction 'SilentlyContinue' | Where { $_.Attributes -match "ReparsePoint"} | % { [System.IO.Directory]::Delete($_.FullName, $true) }
}        
