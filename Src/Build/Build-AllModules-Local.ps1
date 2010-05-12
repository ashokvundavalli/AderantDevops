
param([Array]$modulesToBuild)

begin{

    [Array]$orderedModules
    if ($modulesToBuild){
        $orderedModules = $modulesToBuild
    }else{
        $orderedModules= @('Build.T4Task','Libraries.Models','Libraries.SoftwareFactory','Libraries.Query','Libraries.Foundation','Libraries.Integration','Libraries.Presentation','Services.Foundation','Libraries.Entities.Firm','Services.Communication','Services.Configuration','Services.Identity','Services.Integration','Services.Lookup','Services.Monitoring','Services.Query','Services.Security','Libraries.Entities.BusinessEntity','Libraries.Entities.TaskBasedBilling','Libraries.Packaging','Services.Notes','Libraries.Entities.Client','Libraries.Entities.Marketing','Libraries.Workflow','Services.Applications.FirmControl','Applications.Workflow','Libraries.Entities.Matter','SDK','Libraries.Customization','Packages','Applications.Customization','Libraries.Deployment','Applications.Deployment','Applications.ExpertAssistant','Libraries.Roles','SDK.API','SDK.Workflow','Applications.Administration','Libraries.Entities.Employee','Libraries.Entities.GeneralLedger','Services.Applications.EmployeeIntake','Services.Applications.FileOpening','Workflow.EmployeeIntake','Workflow.FileOpening','Workflow.Samples')                                    
        #$orderedModules= @('Services.Query', 'Services.Notes', 'Libraries.Workflow')                                    
    }    
                   
    Function LoadLibraries(){                    
        $shell = (Join-Path $BuildScriptsDirectory \Build-Libraries.ps1 )        
        &($shell)
    }
    
    Function GetRequiredProductThirdpartyModules(){
       [xml]$product  = Get-Content $ProductManifest
       
       foreach($module in $product.ProductManifest.Modules.SelectNodes("Module")){                
            if((IsThirdparty $module) -or (IsHelp $module)){                                          
                
                $localCopyOfThirdPartyModule = Join-Path -Path $BranchModulesDirectory -ChildPath $module.Name
                
                if(Test-Path $localCopyOfThirdPartyModule){
                    $path = LocalPathToThirdpartyBinariesFor $module $BranchModulesDirectory
                }else{
                    $path = GetPathToBinaries $module $BranchServerDirectory
                }
                CopyContents $path $BranchBinariesDirectory    
                              
            }                                                                         
        }
    }
        
    Function global:GenerateFactory([string]$inDirectory, [string]$searchPath){
        write "Generating factory in [$inDirectory]"        
        &$inDirectory\FactoryResourceGenerator.exe /f:$inDirectory /of:$inDirectory/Factory.bin $searchPath                        
    }
    
    Function BuildAndReportErrors{
        $buildErrors = (bm | Out-String | Where-Object {($_.Contains("0 Error(s)") -eq $false)} )
        if(![string]::IsNullOrEmpty($buildErrors)){
            Write-Host $buildErrors                 
            Write-Host -ForegroundColor red "Build failed for [$CurrentModuleName] as part of the branch build"
            exit           
        }
    }    
                    
}
process{
    LoadLibraries

    [DateTime]$startDate = [DateTime]::Now
    $start = "Start All: $startDate"
    write $start
    write "" 
    
    foreach($module in $orderedModules){
        cm $module
        Write-Host "Building ....."
        gdl -ErrorAction Stop | BuildAndReportErrors -ErrorAction stop | cb -ErrorAction Stop
        Write-Host "Build complete for $CurrentModuleName" 
    }
  
    GetRequiredProductThirdpartyModules 
   
    GenerateFactory $BranchBinariesDirectory "/sp:Aderant*.dll`,*.exe"    
    RemoveReadOnlyAttribute $BranchBinariesDirectory
    write ""        
    [TimeSpan]$diff = [DateTime]::Now.Subtract($startDate)    
    $finish = "Finished all builds: " + $diff.TotalMinutes + " Minutes."
    write $finish
}