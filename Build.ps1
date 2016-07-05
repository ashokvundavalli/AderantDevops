$paket = "$PSScriptRoot\Src\Build.Tools\paket.exe"

& $paket restore

$properties = [PSCustomObject]@{
    PackagesPath = "$PSScriptRoot\packages"
    RepackTool = "$PSScriptRoot\packages\ILRepack\tools\ILRepack.exe"
}

$asm = [System.Reflection.Assembly]::LoadFrom("$PSScriptRoot\Src\Sources\Aderant.Build.Analyzer\bin\Aderant.Build.Analyzer.dll")

$results = .\Get-RuntimeDependencies.ps1 -assembly $asm -packagesPath $properties.PackagesPath

$uri = new-object System.Uri $asm.CodeBase

$assemblies = [string]::Join(" ", ($results.References.Values.GetEnumerator() | % { '"{0}"' -f (new-object System.Uri $_).LocalPath }))
$referencePaths = [string]::Join(" ", ($results.ReferencePaths.GetEnumerator() | % { '/lib:"{0}"' -f $_ }))

iex "$($properties.RepackTool) $($uri.LocalPath) $assemblies $referencePaths /out:Aderant.Build.Analyzer.dll"

#.\packages\ILRepack\tools\ILRepack.exe C:\Source\Build.Infrastructure\Src\Sources\Aderant.Build.Analyzer\bin\Aderant.Build.Analyzer.dll C:\Source\Build.Infrastructure\packages\System.Collections.Immutable\lib\netstandard1.0\System.Collections.Immutable.dll /lib:C:\Source\Build.Infrastructure\packages\Microsoft.CodeAnalysis.Common\lib\net45 /lib:C:\Source\Build.Infrastructure\packages\Microsoft.CodeAnalysis.CSharp.Workspaces\lib\net45 /lib:C:\Source\Build.Infrastructure\packages\Microsoft.CodeAnalysis.Workspaces.Common\lib\net45 /out:Analzyer.dll