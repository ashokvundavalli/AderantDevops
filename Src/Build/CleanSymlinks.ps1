{
[CmdletBinding()]
Param ($path)
         
Process {
    dir -Path $path -Force -Recurse -ErrorAction 'SilentlyContinue' | Where { $_.Attributes -match "ReparsePoint"} | % { [System.IO.Directory]::Delete($_.FullName) }
}
          
}