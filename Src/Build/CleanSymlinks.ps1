[CmdletBinding()]
Param ($path)
         
Process {
    dir -Path $path -Force -Recurse -ErrorAction 'SilentlyContinue' | Where { $_.Attributes -match "ReparsePoint"} | % { Remove-Item $_.FullName -Verbose -Force }
}        
