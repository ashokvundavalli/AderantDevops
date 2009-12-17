<# 
.Synopsis 
    Functions relating to be modules 
.Example     
        
.Remarks
#>    
    
cls

. PSUnit.ps1
. (Join-Path -Path $env:PSUNIT_HOME -ChildPath "..\..\Build.Infrastructure\Src\Build\Build-Libraries.ps1" )

# base paths
$testAssestDir = (Join-Path (Get-FullLocalBranchRootPath) "\Modules\Build.Infrastructure\Test\TestAssets")
$buildInfrastructureSrcBuildDir = (Join-Path (Get-FullLocalBranchRootPath) "\Modules\Build.Infrastructure\Src\Build")
$fakeLocalRoot = (Join-Path $testAssestDir "\Modules")
$moduleTestRoot = $testAssestDir 
$fakeDropRoot =  (Join-Path $testAssestDir "\Drop")
$manifestPath = (Join-Path  $fakeLocalRoot  "\Services.Sample\Build")
[xml]$script:unitTestManifest = Get-Content (Join-Path $testAssestDir "\UnitTestManifest.xml")

write "Build test dir [$buildTestDirectory]"
write "Fake drop root [$fakeDropRoot]"
write "Fake local root [$fakeLocalRoot]"
write "Test Assests dir [$testAssestDir]"

#setup copy current Src dir into test assest modules dir
Remove-Item -Path (Join-Path $fakeLocalRoot "Build.Infrastructure\*") -Force -Recurse
Remove-Item -Path (Join-Path $fakeDropRoot "Build.Infrastructure\*") -Force -Recurse
Copy-Item -Path $buildInfrastructureSrcBuildDir -Destination (Join-Path $fakeLocalRoot "Build.Infrastructure\Src\Build" ) -Force -Recurse
Copy-Item -Path $buildInfrastructureSrcBuildDir -Destination (Join-Path $fakeDropRoot "Build.Infrastructure\Src\Build" ) -Force -Recurse

<#
#This test fives false positives.  If -eq is used to compare diff xml files it returns True!!
Function Test.LoadManifest_ReturnsADependancyManifestContent{    
    [xml]$actual = LoadManifest $manifestPath
    [xml]$expected = Get-Content ($manifestPath + "\DependencyManifest.xml")
    Assert-That -ActualValue $actual -Constraint {$ActualValue.ReferencedModules.InnerXml -eq $expected.ReferencedModules.InnerXml}

}
#>


<#
Function Test.LoadManifest_ReturnsADependancyManifestAsXml{    
    [xml]$actual = LoadManifest $manifestPath
    Assert-That -ActualValue $actual -Constraint {$ActualValue -is [xml]}
}

Function Test.CopyModuleBuildFiles_CopiesDropBuildAssestToTheDestinationDirectory{       
    $toPath = (Join-Path -Path $fakeLocalRoot -ChildPath \Services.Sample\Build)        
    CopyModuleBuildFiles $fakeDropRoot $toPath         
    $testFilePath =  $toPath + "\BuildModule.ps1"
    $actual = (Get-Item -Path $testFilePath).Name                                
    Assert-That -ActualValue $actual -Constraint {[string]::IsNullOrEmpty([string]$ActualValue) -eq $false}
}

Function Test.CopyModuleBuildFiles_CopiesModuleBuildProjFileIntoModuleBuildDirectory{       
    $toPath = (Join-Path $fakeLocalRoot "\Services.Sample\Build")            
    $originalModuleBuildProjFile = (Join-Path $branchRoot "\Modules\Build.Infrastructure\Src\Build\ModuleBuild.proj")
    
    CopyModuleBuildFiles $fakeDropRoot $toPath         
    $moduleBuildProj =  $toPath + "\ModuleBuild.proj"
    [xml]$copiedContent = Get-Content $moduleBuildProj                            
    [xml]$originalContent = Get-Content $originalModuleBuildProjFile      
    $actual = $copiedContent.InnerText.Equals($originalContent.InnerText)
                                      
    Assert-That -ActualValue $actual -Constraint {$ActualValue -eq $true}
}

Function Test.CopyModuleBuildFiles_CopiesLocalBuildAssestToTheDestinationDirectory{   
    $fromPath = $fakeLocalRoot 
    $toPath = (Join-Path -Path $fakeLocalRoot -ChildPath \Services.Sample\Build)        
    CopyModuleBuildFiles $fromPath $toPath         
    $testFilePath =  $toPath + "\BuildModule.ps1"    
    $actual = (Get-Item -Path $testFilePath).Name       
    Assert-That -ActualValue $actual -Constraint {[string]::IsNullOrEmpty([string]$ActualValue) -eq $false}
}#>

Function Test.ChangeBranch_WillChangeTheDropPathBranchFromWorkflowToVS2010{       
    $changedRoot = ChangeBranch "\\na.aderant.com\ExpertSuite\Dev\Workflow" "Dev\VS2010"      
    Write-Debug "Drop Path to Thirdparty.DevComponents is [$changedRoot]"      
    [string]$expetedRoot = "\\na.aderant.com\ExpertSuite\Dev\VS2010" 
    Assert-That -ActualValue $changedRoot.ToLower() -Constraint {([string]$ActualValue).Equals($expetedRoot.ToLower()) -eq $true}      
}

Function Test.ChangeBranch_WillChangeTheDropPathBranchFromWorkflowToMAIN{       
    $changedRoot = ChangeBranch "\\na.aderant.com\ExpertSuite\Dev\Workflow" "MAIN"      
    Write-Debug "Drop Path to Thirdparty.DevComponents is [$changedRoot]"      
    [string]$expetedRoot = "\\na.aderant.com\ExpertSuite\MAIN" 
    Assert-That -ActualValue $changedRoot.ToLower() -Constraint {([string]$ActualValue).Equals($expetedRoot.ToLower()) -eq $true}      
}
<#
Function Test.ChangeBranch_InvalidBranchWillHaveException([switch] $Category_ParameterValidation, [System.IO.DirectoryNotFoundException] $ExpectedException = $Null){       
    $changedRoot = ChangeBranch (Get-DropRootPath) "Junk"      
    Write-Debug "Drop Path to Thirdparty.DevComponents is [$changedRoot]"      
    $expetedRoot = "\\na.aderant.com\ExpertSuite\Dev\VS2010" 
    Assert-That -ActualValue $changedRoot -Constraint {([string]$ActualValue).Equals([string]$expetedRoot) -eq $true}      
}

Function Test.FindGetActionTag_DefaultIsServerDropAderantModules{        
    [xml]$manifest = LoadManifest $manifestPath
    $librariesSampleModule = $manifest.DependencyManifest.ReferencedModules.GetElementsByTagName("ReferencedModule") | Where-Object {if($_.Name){$_.Name.Contains("Libraries.Sample")}}      
    [string]$actual = FindGetActionTag $librariesSampleModule    
    $expected = "serverdrop"
    Assert-That -ActualValue $actual -Constraint {($ActualValue).Equals($expected) -eq $true}
}

Function Test.FindGetActionTag_DefaultIsThirdpartyForThirdpartyModules{        
    [xml]$manifest = LoadManifest $manifestPath
    $thirdpartyDevComponentsModule = $manifest.DependencyManifest.ReferencedModules.GetElementsByTagName("ReferencedModule") | Where-Object {if($_.Name){$_.Name.Contains("Thirdparty.DevComponents")}}      
    [string]$actual = FindGetActionTag $thirdpartyDevComponentsModule    
    $expected = "thirdparty"
    Assert-That -ActualValue $actual -Constraint {($ActualValue).Equals($expected) -eq $true}
}

Function Test.FindGetActionTag_ThirdpartyTestReturnsLocalWhenActionSetAsLocal{        
    [xml]$manifest = LoadManifest $manifestPath
    $thirdpartyTestModule = $manifest.DependencyManifest.ReferencedModules.GetElementsByTagName("ReferencedModule") | Where-Object {if($_.Name){$_.Name.Contains("Thirdparty.Test")}}      
    [string]$actual = FindGetActionTag $thirdpartyTestModule    
    $expected = "Local"
    Assert-That -ActualValue $actual -Constraint {($ActualValue).Equals($expected) -eq $true}
}

Function Test.FindGetActionTag_LibrariesPresentationReturnsBranch{        
    [xml]$manifest = LoadManifest $manifestPath
    $librariesPresentationModule = $manifest.DependencyManifest.ReferencedModules.GetElementsByTagName("ReferencedModule") | Where-Object {if($_.Name){$_.Name.Contains("Libraries.Presentation")}}      
    [string]$actual = FindGetActionTag $librariesPresentationModule    
    $expected = "branch"
    Assert-That -ActualValue $actual -Constraint {($ActualValue).Equals($expected) -eq $true}
}

Function Test.BranchBinariesPathFor_LibrariesPresentationBinModuleComesFromTheVS2010Branch{   
    [xml]$manifest = LoadManifest $manifestPath
    $librariesPresentationModule = $manifest.DependencyManifest.ReferencedModules.GetElementsByTagName("ReferencedModule") | Where-Object {if($_.Name){$_.Name.Contains("Libraries.Presentation")}}          
    
    $actualPath = BranchBinariesPathFor $librariesPresentationModule  
     Write-Debug "Local Path to Libraries.Presentation is [$actualPath]"   
    $isVS2010Path = $actualPath.Contains("VS2010\Libraries.Presentation") 
    Assert-That -ActualValue $isVS2010Path -Constraint {$ActualValue -eq $true}    
}

Function Test.ServerBinariesPathFor_LibrariesPackagingGivesVersionedCopyFromTheDrop{   
    [xml]$manifest = LoadManifest $manifestPath
    $librariesPackagingModule = $manifest.DependencyManifest.ReferencedModules.GetElementsByTagName("ReferencedModule") | Where-Object {if($_.Name){$_.Name.Contains("Libraries.Packaging")}}                  
    $actualPath = ServerBinariesPathFor $librariesPackagingModule        
     Write-Debug "Path to Libraries.Packaging is [$actualPath]"      
    #coming from the drop so we can't guarentee the FileVersion
    
    $isVersionedPath = $actualPath.Contains((Join-Path (Get-DropRootPath) "Libraries.Packaging\1.8.0.0\"))
    Assert-That -ActualValue $isVersionedPath -Constraint {$ActualValue -eq $true}        
}

Function Test.ThirdpartyBinariesPathFor_ThirdpartyDevComponentsWithNoGetActionGivesBin{   
    [xml]$manifest = LoadManifest $manifestPath
    $thirdpartyTestModule = $manifest.DependencyManifest.ReferencedModules.GetElementsByTagName("ReferencedModule") | Where-Object {if($_.Name){$_.Name.Contains("Thirdparty.DevComponents")}}          
    $actualPath = ThirdpartyBinariesPathFor $thirdpartyTestModule    
    $expetedPath = (Join-Path (Get-DropRootPath) \Thirdparty.DevComponents\Bin)
    Write-Debug "Drop Path to Thirdparty.DevComponents is [$actualPath]"      
    Assert-That -ActualValue $actualPath -Constraint {([string]$ActualValue).Equals([string]$expetedPath) -eq $true}      
}

Function Test.ServerBinariesPathFor_librariesPackagingFromDropRetunedWhenPassingADropLocationGivenWhenPassingDropRoot{   
    [xml]$manifest = LoadManifest $manifestPath
    $drop = (Get-DropRootPath) #has to be real drop as it get the file version
    $librariesPackagingModule = $manifest.DependencyManifest.ReferencedModules.GetElementsByTagName("ReferencedModule") | Where-Object {if($_.Name){$_.Name.Contains("Libraries.Packaging")}}                  
    $actualPath = ServerBinariesPathFor $librariesPackagingModule $drop      
    Write-Debug "Versioned Drop Path to Libraries.Packaging is [$actualPath]"      
    #coming from the drop so we can't guarentee the FileVersion
    $isVersionedPathFromDrop = $actualPath.Contains((Join-Path $drop "Libraries.Packaging\1.8.0.0\"))
    Assert-That -ActualValue $isVersionedPathFromDrop -Constraint {$ActualValue -eq $true}        
}

Function Test.ThirdpartyBinariesPathFor_DropThirdPartyGivenWhenPassingDropRoot{   
    [xml]$manifest = LoadManifest $manifestPath
    $drop = "\\bogus.drop\dev\workflow"
    $thirdpartyTestModule = $manifest.DependencyManifest.ReferencedModules.GetElementsByTagName("ReferencedModule") | Where-Object {if($_.Name){$_.Name.Contains("Thirdparty.DevComponents")}}          
    $actualPath = ThirdpartyBinariesPathFor $thirdpartyTestModule $drop
    Write-Debug "Drop Path to Thirdparty.DevComponents is [$actualPath]"      
    $expetedPath = (Join-Path $drop \Thirdparty.DevComponents\Bin)    
    Assert-That -ActualValue $actualPath -Constraint {([string]$ActualValue).Equals([string]$expetedPath) -eq $true}      
}

Function Test.IsThirdparty_WhenTheNameContainsThirdparty_True{    
    $thirdPartyTest = $unitTestManifest.DependencyManifest.ReferencedModules.GetElementsByTagName("ReferencedModule") | Where-Object {if($_.Name){$_.Name.Contains("Thirdparty.Test")}}
    $actual = IsThirdparty $thirdPartyTest
    Assert-That -ActualValue $actual -Constraint {$ActualValue -eq $true}
}

Function Test.IsThirdparty_WhenTheNameContainsThirdpartyMixedCase_True{    
    $thirdPartyStuff = $unitTestManifest.DependencyManifest.ReferencedModules.GetElementsByTagName("ReferencedModule") | Where-Object {if($_.Name){$_.Name.Contains("THiRdPArty.Stuff")}}
    $actual = IsThirdparty $thirdPartyStuff
    Assert-That -ActualValue $actual -Constraint {$ActualValue -eq $true}
}

Function Test.IsThirdparty_WhenTheNameDoesNotContainThirdparty_False{    
    $librariesOther = $unitTestManifest.DependencyManifest.ReferencedModules.GetElementsByTagName("ReferencedModule") | Where-Object {if($_.Name){$_.Name.Contains("libraries.other")}}
    $actual = IsThirdparty $librariesOther        
    Assert-That -ActualValue $actual -Constraint {$ActualValue -eq $false}
}
Function Test.IsHelp_WhenTheNameContainsExpertHelp_True{
    $expertHelp = $unitTestManifest.DependencyManifest.ReferencedModules.GetElementsByTagName("ReferencedModule") | Where-Object {if($_.Name){$_.Name.Contains("expert.help")}}
    $actual = IsHelp $expertHelp            
    Assert-That -ActualValue $actual -Constraint {$ActualValue -eq $true}
}

Function Test.IsHelp_WhenTheNameContainsExpertHelpMixedCase_True{
    $expertHelpMixedCase = $unitTestManifest.DependencyManifest.ReferencedModules.GetElementsByTagName("ReferencedModule") | Where-Object {if($_.Name){$_.Name.Contains("exPErt.hELP")}}
    $actual = IsHelp $expertHelpMixedCase
    Assert-That -ActualValue $actual -Constraint {$ActualValue -eq $true}
}

Function Test.IsHelp_WhenTheNameDoesNotContainExpertHelp_False{
    $librariesOther = $unitTestManifest.DependencyManifest.ReferencedModules.GetElementsByTagName("ReferencedModule") | Where-Object {if($_.Name){$_.Name.Contains("libraries.other")}}
    $actual = IsHelp $librariesOther        
    Assert-That -ActualValue $actual -Constraint {$ActualValue -eq $false}
}


Function Test.CopyContents_TakesTheSourceStructureAndReplicatesInTheDestination{   

    $copyTo = (Join-Path -Path $moduleTestRoot -ChildPath \To)
    $copyFrom = (Join-Path -Path $moduleTestRoot -ChildPath \From)
    $copyFromPathFolder = (Join-Path $moduleTestRoot \From\Path)
    $copyFromPathCreationFolder = (Join-Path $moduleTestRoot \From\Path\Creation)        
    $blah1 = "blah1.txt"
    $blah2 = "blah2.txt"   
    
    #remove
    if(Test-Path $copyFromPathCreationFolder){
        Remove-Item $copyFromPathCreationFolder -Force -Recurse
        Remove-Item $copyFromPathFolder -Force -Recurse
    }
    if(Test-Path $copyTo){
        Remove-Item $copyTo -Force -Recurse    
    } 
    #setup
    New-Item -ItemType Directory -Path $copyFromPathCreationFolder    
    New-Item -ItemType file -Path $copyFromPathFolder\$blah1
    New-Item -ItemType file -Path $copyFromPathCreationFolder\$blah2
    
    CopyContents $copyFrom $copyTo   
    $blah2FileExists = (Test-Path (Join-Path $copyTo \Path\Creation\$blah2))
    Assert-That -ActualValue $blah2FileExists -Constraint {$ActualValue -eq $true}      
    
    $blah1FileExists = (Test-Path (Join-Path $copyTo \Path\$blah1))
    Assert-That -ActualValue $blah1FileExists -Constraint {$ActualValue -eq $true}
}

Function Test.CopyContents_RemovesTrailingBackSlashOnPaths{   

    $copyTo = (Join-Path -Path $moduleTestRoot -ChildPath \To\)
    $copyFrom = (Join-Path -Path $moduleTestRoot -ChildPath \From\)    
    
    #remove
    if(Test-Path $copyFrom){
        Remove-Item $copyFrom -Force -Recurse
    }
    if(Test-Path $copyTo){
        Remove-Item $copyTo -Force -Recurse    
    } 
    #setup
    New-Item -ItemType Directory -Path $copyFrom    
    New-Item -ItemType Directory -Path $copyTo
    New-Item -ItemType file -Path $copyFrom\blahblah.txt
    
    CopyContents $copyFrom $copyTo   
    $blahblahFileExists = (Test-Path (Join-Path $copyTo blahblah.txt))
    Assert-That -ActualValue $blahblahFileExists -Constraint {$ActualValue -eq $true}      
}


Function Test.CopyContents_IfCopyToFolderDoenotExistItWillBeCreated{   

    $copyTo = (Join-Path -Path $moduleTestRoot -ChildPath \NoFolder)
    $copyFrom = (Join-Path -Path $moduleTestRoot -ChildPath \From)    
    
    #remove
    if(Test-Path $copyFrom){
        Remove-Item $copyFrom -Force -Recurse
    }
    if(Test-Path $copyTo){
        Remove-Item $copyTo -Force -Recurse    
    } 
    #setup
    New-Item -ItemType Directory -Path $copyFrom        
    
    CopyContents $copyFrom $copyTo   
    $copyToFolderExists = (Test-Path $copyTo )
    Assert-That -ActualValue $copyToFolderExists -Constraint {$ActualValue -eq $true}      
}


Function Test.CopyContents_IfFolderPathHasWhiteSpaceTheItWillBeTrimmed{   

    $copyTo = (Join-Path -Path $moduleTestRoot -ChildPath '\WhiteSpace ' )
    $copyFrom = (Join-Path -Path $moduleTestRoot -ChildPath \From)    
    
    #remove
    if(Test-Path $copyFrom){
        Remove-Item $copyFrom -Force -Recurse
    }
    if(Test-Path $copyTo.Trim()){
        Remove-Item $copyTo.Trim() -Force -Recurse    
    } 
    #setup
    New-Item -ItemType Directory -Path $copyFrom        
    
    CopyContents $copyFrom $copyTo   
    $copyToFolderExists = Test-Path $copyTo.Trim() 
    Assert-That -ActualValue $copyToFolderExists -Constraint {$ActualValue -eq $true}      
}


Function Test.FindLatestSuccessfulBuildFolderName_ReturnsFileVersion181112222{
    $versionRootPath =  (Join-Path -Path $fakeDropRoot -ChildPath \Libraries.Sample\1.8.0.0\)    
    $latestFileVersionPath = FindLatestSuccessfulBuildFolderName $versionRootPath    
    Assert-That -ActualValue $latestFileVersionPath -Constraint {([string]$ActualValue).Contains("1.8.111.2222")}
}


Function Test.ResolveAndCopyUniqueBinModuleContent_CopiesOutputAndOutput2TextFilesAndPreservesStructure{

    $modulePath = Join-Path $fakeLocalRoot \Copy.Module
    $binModuleDir = Join-Path $modulePath \Bin\Module
    $configPath = "\Installation\Configurations\Deployment"
    $testBinConfigDir = Join-Path $binModuleDir $configPath
    $testDependanciesDir = Join-Path $modulePath \Dependencies
    $dropDir = Join-Path $fakeDropRoot \CopyBinFilesFromTest

    #clean up
    if(Test-Path $binModuleDir){
        Remove-Item $binModuleDir -Force -Recurse
    }
    if(Test-Path $testBinConfigDir){
        Remove-Item $testBinConfigDir -Force -Recurse
    }
    if(Test-Path $testDependanciesDir){
        Remove-Item $testDependanciesDir -Force -Recurse    
    } 
    if(Test-Path $dropDir){
        Remove-Item $dropDir -Force -Recurse    
    }
    
    #setup
    New-Item -ItemType Directory -Path $binModuleDir
    New-Item -ItemType Directory -Path $testBinConfigDir
    New-Item -ItemType Directory -Path $testDependanciesDir
    New-Item -ItemType Directory -Path $dropDir
    New-Item -ItemType file -Path $binModuleDir\output.txt
    New-Item -ItemType file -Path $testBinConfigDir\output2.txt
    New-Item -ItemType file -Path $binModuleDir\dependancies1.txt
    New-Item -ItemType file -Path $testDependanciesDir\dependancies1.txt        
    
    ResolveAndCopyUniqueBinModuleContent -modulePath $modulePath -copyToDirectory $dropDir                
         
    $outputExists = Test-Path (Join-Path $dropDir "output.txt")        
    $output2Exists = Test-Path (Join-Path $dropDir $configPath\output2.txt)
    $dependancies1DoesNotExist = !(Test-Path (Join-Path $dropDir "dependancies1.txt"))
        
    Assert-That -ActualValue ($outputExists -and $output2Exists -and $dependancies1DoesNotExist ) -Constraint {$ActualValue -eq $true}
}

Function Test.CopyBinFilesForDrop_ReturnsHasOutputAndOutput2TextFiles{

    $modulePath = Join-Path $fakeLocalRoot \Copy.Module
    $binModuleDir = Join-Path $modulePath \Bin\Module
    $configPath = "\Installation\Configurations\Deployment"
    $testBinConfigDir = Join-Path $binModuleDir $configPath
    $testDependanciesDir = Join-Path $modulePath \Dependencies
    $dropDir = Join-Path $fakeDropRoot \CopyBinFilesForDropTest

    #clean up
    if(Test-Path $binModuleDir){
        Remove-Item $binModuleDir -Force -Recurse
    }
    if(Test-Path $testBinConfigDir){
        Remove-Item $testBinConfigDir -Force -Recurse
    }
    if(Test-Path $testDependanciesDir){
        Remove-Item $testDependanciesDir -Force -Recurse    
    } 
    if(Test-Path $dropDir){
        Remove-Item $dropDir -Force -Recurse    
    }
    
    #setup
    New-Item -ItemType Directory -Path $binModuleDir
    New-Item -ItemType Directory -Path $testBinConfigDir
    New-Item -ItemType Directory -Path $testDependanciesDir
    New-Item -ItemType Directory -Path $dropDir
    New-Item -ItemType file -Path $binModuleDir\output.txt
    New-Item -ItemType file -Path $testBinConfigDir\output2.txt
    New-Item -ItemType file -Path $binModuleDir\dependancies1.txt
    New-Item -ItemType file -Path $testDependanciesDir\dependancies1.txt        
    
    CopyBinFilesForDrop -modulePath $modulePath -toModuleDropPath $dropDir                
    
    $dropBinModulePath = Join-Path $dropDir \Bin\Module
         
    $outputExists = Test-Path (Join-Path $dropBinModulePath "output.txt")        
    $output2Exists = Test-Path (Join-Path $dropBinModulePath $configPath\output2.txt)
    $dependancies1DoesNotExist = !(Test-Path (Join-Path $dropBinModulePath "dependancies1.txt"))
        
    Assert-That -ActualValue ($outputExists -and $output2Exists -and $dependancies1DoesNotExist ) -Constraint {$ActualValue -eq $true}
}



Function Test.CopyBinFilesForDrop_PutsTestFilesIntoFolderMonduleTestAtDropZone{

    $modulePath = Join-Path $fakeLocalRoot \Copy.Module
    $testModuleDir = Join-Path $modulePath \Bin\Test        
    $dropDir = Join-Path $fakeDropRoot \ModuleBinTest

    #clean up
    if(Test-Path $testModuleDir){
        Remove-Item $testModuleDir -Force -Recurse
    }        
    if(Test-Path $dropDir){
        Remove-Item $dropDir -Force -Recurse    
    }
    
    #setup
    New-Item -ItemType Directory -Path $testModuleDir
    New-Item -ItemType Directory -Path $dropDir
    New-Item -ItemType file -Path $testModuleDir\test1.txt
    
    CopyBinFilesForDrop -modulePath $modulePath -toModuleDropPath $dropDir                
    
    $dropTestModulePath = Join-Path $dropDir \Bin\Test
         
    $outputExists = Test-Path (Join-Path $dropTestModulePath "test1.txt") 
        
    Assert-That -ActualValue $outputExists -Constraint {$ActualValue -eq $true}
}#>
