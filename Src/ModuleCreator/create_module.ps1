param(
   [string] $ModuleName = $(throw 'ModuleName is required')
   # If no Destination folder is entered, then it will default to the root folder.
  ,[string] $DestinationFolder
)

$ModuleDir = $ModuleName
$SrcDir = "$ModuleName\Src"
$DocDir = "$ModuleName\Doc"
$BuildDir = "$ModuleName\Build"
$TestDir = "$ModuleName\Test"
$ResourcesDir = (Resolve-Path .\ModuleResources)

#Directory creation
New-Item $DestinationFolder\$ModuleName -type directory
New-Item $DestinationFolder\$DocDir -type directory
New-Item $DestinationFolder\$SrcDir -type directory
New-Item $DestinationFolder\$TestDir -type directory
New-Item $DestinationFolder\$BuildDir -type directory

#Build Items
Copy-Item $ResourcesDir\DependencyManifest.xml -Destination $DestinationFolder\$BuildDir\DependencyManifest.xml
Copy-Item $ResourcesDir\CommonAssemblyInfo.cs -Destination  $DestinationFolder\$BuildDir\CommonAssemblyInfo.cs
Copy-Item $ResourcesDir\CustomDictionary.xml -Destination  $DestinationFolder\$BuildDir\CustomDictionary.xml
Copy-Item $ResourcesDir\TFSBuild.proj -Destination  $DestinationFolder\$BuildDir\TFSBuild.proj 
(Get-Content $ResourcesDir\TFSBuild.rsp) | Foreach-Object {$_ -replace "GivenModuleName", $ModuleName} | Set-Content $DestinationFolder\$BuildDir\TFSBuild.rsp
#Solution Template
(Get-Content $ResourcesDir\TemplateSolution.sln) | Foreach-Object {$_ -replace "CreateGuid", [Guid]::NewGuid()} | Set-Content $DestinationFolder\$ModuleDir\$ModuleName.sln
#TestProject Guid
(Get-Content $DestinationFolder\$ModuleDir\$ModuleName.sln) | Foreach-Object {$_ -replace "CreateTestProjectGuid", $TestProjectGuid} | Set-Content $DestinationFolder\$ModuleDir\$ModuleName.sln
#Doc readme
(Get-Content $ResourcesDir\docReadme.txt) | Set-Content $DestinationFolder\$DocDir\Readme.txt