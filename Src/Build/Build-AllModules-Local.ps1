
param([Array]$modulesToBuild)

begin{

    [Array]$orderedModules
    if ($modulesToBuild){
        $orderedModules = $modulesToBuild
    }else{
        $orderedModules= @('libraries.softwarefactory','Libraries.Foundation','Libraries.Presentation','libraries.packaging','Libraries.Query','services.notes','libraries.workflow','sdk','sdk.workflow','libraries.customization','libraries.deployment','applications.deployment','applications.workflow', 'applications.administration', 'Services.Entities.Client', 'Services.Entities.Matter','Services.Query','applications.expertassistant','Packages')                
    }    
        
    Function CheckBuild([string]$buildLog){
                    
        $noErrors = Get-Content $buildLog | select -last 10 | where {$_.Contains("0 Error(s)")}
                    
        if ([String]::IsNullOrEmpty($noErrors)){      
          $buildError = "Build Failed!! Check the log " + $buildLog                                 
           Write-Error -Message $buildError -ErrorAction Stop         
        }else{        
           return $true
        }
    }
    
    Function CheckDependanciesCopy([string]$dependanciesLog){        
       $log = Get-Content $dependanciesLog           
    
       if($log.ToString().Contains("Resolve-Path : Cannot find path")){                                            
          $buildError = "Copy Dependencies Failed!! Check the log " + $dependanciesLog                                 
          Write-Error -Message $buildError -ErrorAction Stop
       }
       return $true                      
    }
    
    Function DoBuild(){    
      write ""      
      write "Start of build for: " (cm?)      
      $getdependencieslog = Join-Path (Get-BinariesPath) getdependencieslog.txt
      $buildlog = Join-Path (Get-BinariesPath) buildlog.txt
      $copylog = Join-Path (Get-BinariesPath) copylog.txt
      
      
      Get-DependenciesForCurrentModule | Out-File $getdependencieslog      
      if(CheckDependanciesCopy $getdependencieslog){         
         Start-BuildForCurrentModule | Out-File $buildlog
         if(CheckBuild $buildlog){         
            Copy-BinariesFromCurrentModule | Out-File $copylog
         }
      }      
      write "Completion of build for: " (cm?)
      write ""
    }
    
    Function LoadLibraries(){                    
        $shell = (Join-Path (Get-LocalModulesRootPath) \Build.Infrastructure\Src\Build\Build-Libraries.ps1 )        
        &($shell)
    }
    
    Function GetRequiredThirdparty(){
       [xml]$product  = Get-Content (Get-ExpertProductManifest)
       
       foreach($module in $product.ProductManifest.Modules.SelectNodes("Module")){                
            if((IsThirdparty $module) -or (IsHelp $module)){            
                $moduleBinariesDirectory = ThirdpartyBinariesPathFor $module (Get-LocalModulesRootPath)
                CopyModuleBinariesDirectory $moduleBinariesDirectory (Get-BinariesPath)                       
            }                                                                         
        }
    }
        
    Function global:GenerateFactory([string]$inDirectory, [string]$searchPath){
        write "Generating factory in [$inDirectory]"        
        &$inDirectory\FactoryResourceGenerator.exe /f:$inDirectory /of:$inDirectory/Factory.bin $searchPath                        
    }    
            
}
process{
    LoadLibraries

    [DateTime]$startDate = [DateTime]::Now
    $start = "Start All: $startDate"
    write $start
    write "" 
    
    foreach( $module in $orderedModules){
        cm $module
        DoBuild 
    }
    
    GetRequiredThirdparty    
    GenerateFactory (Get-BinariesPath) "/sp:Aderant*.dll`,*.exe"    
    RemoveReadOnlyAttribute (Get-BinariesPath)
    write ""        
    [TimeSpan]$diff = [DateTime]::Now.Subtract($startDate)    
    $finish = "Finished all builds: " + $diff.TotalMinutes + " Minutes."
    write $finish
}