cls

#directories
$currentDirectory = $branchRoot+"\Modules\Build.Infrastructure\Test\ModuleCreator.Test"
$moduleCreateScriptBase = $branchRoot+"\Modules\Build.Infrastructure\Src\ModuleCreator\"
$moduleBaseDirectory = $currentDirectory + "\Output\"
$moduleName = "TestModule"
$moduleDir = $moduleName
$srcDir = "$moduleName\Src"
$docDir = "$moduleName\Doc"
$buildDir = "$moduleName\Build"
$testDir = "$moduleName\Test"

#cleanup 
if(Test-Path $moduleBaseDirectory){
    Remove-Item $moduleBaseDirectory\* -Recurse -Force
}else{
    New-Item $moduleBaseDirectory -ItemType directory
}

#setup
. PSUnit.ps1

$shell = ".\create_module.ps1 $moduleName $moduleBaseDirectory"
    
pushd $moduleCreateScriptBase
invoke-expression $shell
popd



Function Test.CreateModule_AddsDependencyManifestInTheBuildFolder{    
    $dependencyManifestPath = $moduleBaseDirectory + $buildDir + "\DependencyManifest.xml"    
    Write-Debug "dependencyManifestPath = [$dependencyManifestPath]"    
    $dependencyManifestExists = [system.io.file]::Exists($dependencyManifestPath)       
    Assert-That -ActualValue $dependencyManifestExists -Constraint {$ActualValue -eq $true}
}

Function Test.CreateModule_AddsCommonAssemblyInfoInTheBuildFolder{        
    $commonAssemblyPath = $moduleBaseDirectory + $buildDir + "\CommonAssemblyInfo.cs"
    Write-Debug "commonAssemblyPath = [$commonAssemblyPath]"
    $commonAssemblyInfoExists = [system.io.file]::Exists($commonAssemblyPath)       
    Assert-That -ActualValue $commonAssemblyInfoExists -Constraint {$ActualValue -eq $true}
}

Function Test.CreateModule_AddsCustomDictionaryInTheBuildFolder{        
    $customDictionaryPath = $moduleBaseDirectory + $buildDir + "\CustomDictionary.xml"
    Write-Debug "customDictionaryPath = [$customDictionaryPath]"
    $customDictionaryExists = [system.io.file]::Exists($customDictionaryPath)       
    Assert-That -ActualValue $customDictionaryExists -Constraint {$ActualValue -eq $true}
}

Function Test.CreateModule_AddsTFSBuildrspInTheBuildFolder{        
    $tfsBuildRspPath =  $moduleBaseDirectory + $buildDir + "\TFSBuild.rsp"
    Write-Debug "tfsBuildRspPath = [$tfsBuildRspPath]"
    $tfsBuildRspExists = [system.io.file]::Exists($tfsBuildRspPath)       
    Assert-That -ActualValue $tfsBuildRspExists -Constraint {$ActualValue -eq $true}
}

Function Test.CreateModule_AddsTFSBuildProjInTheBuildFolder{        
    $tfsBuildRspPath =  $moduleBaseDirectory + $buildDir + "\TFSBuild.proj"
    Write-Debug "tfsBuildRspPath = [$tfsBuildRspPath]"
    $tfsBuildRspExists = [system.io.file]::Exists($tfsBuildRspPath)       
    Assert-That -ActualValue $tfsBuildRspExists -Constraint {$ActualValue -eq $true}
}


Function Test.CreateModule_AddsASolutionFileCalledTestModuleTheModuleFolder{        
    $testModuleSlnPath = $moduleBaseDirectory + $moduleDir + "\$moduleName.sln"
    Write-Debug "testModuleSlnPath = [$testModuleSlnPath]"
    $testModuleSlnExists = [system.io.file]::Exists($testModuleSlnPath)       
    Assert-That -ActualValue $testModuleSlnExists -Constraint {$ActualValue -eq $true}
}

Function Test.CreateModule_AddReadMeInTheDocDir{            
    $readMePath = $moduleBaseDirectory + $docDir + "\Readme.txt"
    Write-Debug "readMePath = [$readMePath]"
    $readMeExists = [system.io.file]::Exists($readMePath)       
    Assert-That -ActualValue $readMeExists -Constraint {$ActualValue -eq $true}
}

Function Test.CreateModule_CreatesATestDir{            
    $testDirExist = Test-Path ($moduleBaseDirectory + $testDir)
    Assert-That -ActualValue $testDirExist -Constraint {$ActualValue -eq $true}
}