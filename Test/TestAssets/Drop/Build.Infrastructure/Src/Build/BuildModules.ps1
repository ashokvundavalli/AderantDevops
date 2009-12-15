##
# Build all modules defined in the product manifest.
# Will optionaly get the modules dependancies from the drop location.
##
param(     
    [string]$productManifestPath ,
    [bool]$getDependencies = $false, 
    [string]$dropRootUNCPath = $(if($getDependencies) {Throw "Parameter missing -dropRootUNCPath dropRootUNCPath" })
)

begin{

    if($productManifestPath){
        [string]$resolvedproductManifestPath = Resolve-Path $productManifestPath

        if([System.IO.File]::Exists($resolvedproductManifestPath)){
            [xml]$product =  Get-Content $resolvedproductManifestPath
                                
            foreach($module in $product.ProductManifest.Modules.SelectNodes("Module")){            
                .\BuildModule.ps1 $module.Name $getDependencies $dropRootUNCPath                             
            }            
        }
        else{
            $(Throw "File Not Found $productManifestPath" ) 
        }                                    
    }                     
}