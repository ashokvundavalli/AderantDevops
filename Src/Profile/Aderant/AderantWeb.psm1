<#
    This file is intended for putting all the functions which are specific for web development.
#>

<# 
.Synopsis 
    Copy Local Foundation and Presentation Shared Files to Current Module for Web Modules
.Description   
    Copies Local Web.Foundation and/or Web.Presentation Shared Files to Current Modules
    (incl. Web.Presentation which gets files from Web.Foundation only)
    Involves file types of .LESS, .CSS, .JS, .DTS, .CSHTML
    Involves .DLL files if -dlls option is enabled
.PARAMETER dlls
    Switch - boolean value to enable/disable the file copying of DLLs
.EXAMPLE
    (When current module is Services.Query)
    Copy-SharedWebFilesToCurrentModule
    Will show warning message that this command does not work for modules that is not a Web Module.  

    Copy-SharedWebFilesToCurrentModule -dlls
    Will copy all shared files (.LESS, .CSS, .JS, .DTS, .CSHTML, and .DLL) from local Web.Foudnation to the corresponding folders in Web.Case.  

    (When current module is Web.Presentation)
    Copy-SharedWebFilesToCurrentModule
    Will copy all shared files (.LESS, .CSS, .JS, .DTS, .CSHTML) from local Web.Foudnation to the corresponding folders in Web.Case.  

    Copy-SharedWebFilesToCurrentModule -dlls
    Will copy all shared files (.LESS, .CSS, .JS, .DTS, .CSHTML, and .DLL) from local Web.Foudnation to the corresponding folders in Web.Case.  

    (When current module is Web.Case)
    Copy-SharedWebFilesToCurrentModule
    Will copy all shared files (.LESS, .CSS, .JS, .DTS, .CSHTML) from local Web.Foudnation and Web.Presentation to the corresponding folders in Web.Case.  

    Copy-SharedWebFilesToCurrentModule -dlls
    Will copy all shared files (.LESS, .CSS, .JS, .DTS, .CSHTML, and .DLL) from local Web.Foudnation and Web.Presentation to the corresponding folders in Web.Case.  
    
    The user must manually fix the .csproj file if there are new file(s) added in the source modules (Foundation/Presentation).
#>
function Copy-SharedFilesToCurrentWebModule {
    param ([switch] $dlls = $false, [switch] $css = $false)

    # check if current module is null
    if ([string]::IsNullOrEmpty($global:CurrentModulePath)) {    
        Write-Warning "The current module is not set so the binaries will not be copied";
    } else {
        # check if current module is a web module and is not Web.Foundation
        if($global:CurrentModuleName.StartsWith("Web", "CurrentCultureIgnoreCase") -and !$global:CurrentModuleName.EndsWith("Foundation", "CurrentCultureIgnoreCase")){
            $elapsed = [System.Diagnostics.Stopwatch]::StartNew();
        
            $TargetDependenciesDirectory = "$global:BranchModulesDirectory\$global:CurrentModuleName\Dependencies";
            $TargetSrcDirectory = "$global:BranchModulesDirectory\$global:CurrentModuleName\Src\$global:CurrentModuleName";
                
            # Part X: manually set up file paths for xcopy - this needs to be improved
            $SharedModuleArray = "Web.Foundation", "Web.Presentation", "Web.SMB";
            $SubFolderArray = "Content", "Scripts", "ViewModels", "View\Shared";
 
            if($css) {
                if(($global:CurrentModuleName.EndsWith("Foundation")) -or ($global:CurrentModuleName.EndsWith("Presentation"))){
                    Write-Debug "Skipping..";
                } else {
                    $CSSSourcePath = "$global:BranchModulesDirectory\Web.Presentation\Src\Web.Presentation\Content\Web.Presentation\*";
                    $CSSTargetDependenciesPath = "$TargetDependenciesDirectory\Web.Presentation\Content\Web.Presentation\*";
                    $CSSTargetSrcPath = "$TargetSrcDirectory\Content\Web.Presentation\*";
                    xcopy $CSSSourcePath $CSSTargetDependenciesPath /y /f /D /U /S;
                    xcopy $CSSSourcePath $CSSTargetSrcPath /y /f /D /U /S;
                }
            } else{
                foreach ($sourceModule in $SharedModuleArray) {
                    $SourcePath = "$global:BranchModulesDirectory\$sourceModule\Src\$sourceModule";
                    if((Test-Path "$TargetDependenciesDirectory\$sourceModule") -or ($sourceModule.EndsWith("SMB"))){
                        if((($global:CurrentModuleName.EndsWith("Presentation")) -and (!$sourceModule.EndsWith("Foundation"))) -or (($global:CurrentModuleName.EndsWith("SMB")) -and ((!$sourceModule.EndsWith("Foundation")) -or (!$sourceModule.EndsWith("Presentation"))))) {
                            Write-Debug "Skipping..";
                        } else {
                            Write-Host "Starting Xcopy for $sourceModule/:";
                            xcopy "$SourcePath\*" "$TargetDependenciesDirectory\$sourceModule" /y /f /D /U /S;
                if ($dlls) {
                                xcopy "$SourcePath\bin\Web.*.???" "$TargetDependenciesDirectory" /y /f /D /U /S;
                                Write-Debug "Dlls copy completed in $sourceModule.";
                }
                            Write-Debug "Dependencies copy completed in $sourceModule.";
                            foreach ($folder in $SubFolderArray) {
                                if(($sourceModule.EndsWith("Foundation")) -and ($folder.EndsWith("Content"))){
                                    Write-Debug "Skipping...";
                                } else {
                                    xcopy "$SourcePath\$folder\$sourceModule\*" "$TargetSrcDirectory\$folder\$sourceModule" /y /f /D /U /S;
                                    Write-Debug "$folder copy completed in $sourceModule.";
            }
            }
                        }
                    } else {
                        Write-Error "$sourceModule folder does not exist in .\Dependencies under your current module, please run a Get-Dependencies.";
                }
            }
            }
            Write-Host "Total Elapsed Time: $($elapsed.Elapsed.ToString())";
        
        # if the current module is not a web module, or it is Web.Foundation, then a warning message will be shown
        } else {
            Write-Warning "Sorry, this command will only work for Web Modules (excl. Web.Foundation)";
        }
    }  
}

<# 
.Synopsis 
    Compile Aderant.*.less files for the current module and put them into pending changes
.Description   
    Compile Aderant.*.less for Aderant.*.css and Aderant.*.css.map (if it is a SMB module) for the current module
    Involves changes in file types of .LESS in Web Projects
.PARAMETER all
    Switch - boolean value to enable/disable whether to compile all the 7 Aderant.*.less in branch, this includes Web.Case, Web.Financials, Web.Administration, Web.Test, Web.Time, Web.Expenses, and Web.Workflow
.PARAMETER smb
    Switch - boolean value to enable/disable whether to compile all the 4 Aderant.SMB.less in our branch, this includes Web.Case, Web.Financials, Web.Administration, and Web.Test
.PARAMETER otg
    Switch - boolean value to enable/disable whether to compile all the 3 OTG Aderant.*.less in our branch, this includes Web.Time, Web.Expenses, and Web.Workflow
.PARAMETER gdf
    Switch - boolean value to enable/disable whether to run a gdf in ExpressMode before start compiling the LESS file(s)
.PARAMETER install
    Switch - boolean value to enable/disable whether to run the installation for the prerequisites first, this includes Node.js NPM and LessCss compiler
.EXAMPLE
    (When current module is Services.Query)
    Copy-SharedWebFilesToCurrentModule
    Will show warning message that this command does not work for modules that is not a Web Module.  

    Copy-SharedWebFilesToCurrentModule -dlls
    Will copy all shared files (.LESS, .CSS, .JS, .DTS, .CSHTML, and .DLL) from local Web.Foudnation to the corresponding folders in Web.Case.  

    (When current module is Web.Presentation)
    Copy-SharedWebFilesToCurrentModule
    Will copy all shared files (.LESS, .CSS, .JS, .DTS, .CSHTML) from local Web.Foudnation to the corresponding folders in Web.Case.  

    Copy-SharedWebFilesToCurrentModule -dlls
    Will copy all shared files (.LESS, .CSS, .JS, .DTS, .CSHTML, and .DLL) from local Web.Foudnation to the corresponding folders in Web.Case.  

    (When current module is Web.Case)
    Copy-SharedWebFilesToCurrentModule
    Will copy all shared files (.LESS, .CSS, .JS, .DTS, .CSHTML) from local Web.Foudnation and Web.Presentation to the corresponding folders in Web.Case.  

    Copy-SharedWebFilesToCurrentModule -dlls
    Will copy all shared files (.LESS, .CSS, .JS, .DTS, .CSHTML, and .DLL) from local Web.Foudnation and Web.Presentation to the corresponding folders in Web.Case.  
    
    The user must manually fix the .csproj file if there are new file(s) added in the source modules (Foundation/Presentation).
#>
function Compile-LessFilesForWebProjects() {
    param ([switch] $all = $false, [switch] $smb = $false, [switch] $otg = $false, [switch] $gdf = $false, [switch] $install = $false)

    function CompileLess {
        
        if ($gdf) {
            Invoke-Expression "Copy-SharedFilesToCurrentWebModule -css"
        }

        # Part 1: set up root directories
        $CurrentModule = $global:CurrentModuleName
        $CurrentWebProject = $CurrentModule.split('.')[1]
        $ContentDirectoryFullPath = "$global:BranchModulesDirectory\$CurrentModule\Src\$CurrentModule\Content"
        
        if ($CurrentWebProject.StartsWith("Case", "CurrentCultureIgnoreCase") -or $CurrentWebProject.StartsWith("Administration", "CurrentCultureIgnoreCase") -or $CurrentWebProject.StartsWith("Financials", "CurrentCultureIgnoreCase") -or $CurrentWebProject.StartsWith("Test", "CurrentCultureIgnoreCase")) {
            $CurrentWebProject = "SMB"
        }
        
        $AderantLess = "$ContentDirectoryFullPath\Aderant.$CurrentWebProject.less"
        $AderantCss = "$ContentDirectoryFullPath\Aderant.$CurrentWebProject.css"
        $AderantCssMap = "$ContentDirectoryFullPath\Aderant.$CurrentWebProject.css.map"
        Write-Host "Current Module Content directory has been set up..."

        Invoke-Expression "tf checkout $AderantCss"
        if ($CurrentWebProject.StartsWith("SMB", "CurrentCultureIgnoreCase")) {
            Invoke-Expression "tf checkout $AderantCssMap"
            Invoke-Expression "lessc -ru --source-map=Aderant.$CurrentWebProject.css.map $AderantLess > $AderantCss"
        } else {
            Invoke-Expression "lessc -ru $AderantLess > $AderantCss"
        }
    }
    
    function CompileSMBAll {
        $gdf = -not $gdf

        Invoke-Expression "cm Web.Case"
        CompileLess
        
        Invoke-Expression "cm Web.Administration"
        CompileLess

        Invoke-Expression "cm Web.Financials"
        CompileLess
        
        Invoke-Expression "cm Web.Test"
        CompileLess
    }
    
    function CompileOTGAll {
        $gdf = -not $gdf

        Invoke-Expression "cm Web.Time"
        CompileLess
        
        Invoke-Expression "cm Web.Expenses"
        CompileLess

        Invoke-Expression "cm Web.Workflow"
        CompileLess
    }
    
    function SilentlyInstallNPM {
        Write-Warning "Please wait while Nodejs is being installed, this may take up to a minute..."
        cmd /c msiexec /i "http://nodejs.org/dist/v0.10.29/node-v0.10.29-x86.msi" /qn

        $title = "A restart of PowerShell is needed."
        $message = "Please re-open your PowerShell Window... "

        $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes", `
            "Yes"

        $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No", `
            "No"

        $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)

        $result = $host.ui.PromptForChoice($title, $message, $options, 0) 

        switch ($result) {
            0 {Exit}
            1 {Write-Warning "You selected No... Please restart PowerShell manually when you are ready."}
        }
    }
    
    function SilentlyInstallLESSC {
        npm install -g less
    }
    
    if (Get-Command npm -errorAction SilentlyContinue) {
        Write-Host "Node.js and NPM exists, good to go!"
    } else {
        SilentlyInstallNPM
    }
    
    if (Get-Command lessc -errorAction SilentlyContinue) {
        Write-Host "LESS Compiler lessc exists, good to go!"
    } else {
        SilentlyInstallLESSC
    }
    
    if($install) {
        break
    }
    
    # check if current module is null
    if ([string]::IsNullOrEmpty($global:CurrentModulePath) -and !$all) {    
        Write-Warning "The current module is not set so the binaries will not be copied"
    } else {
        # check if current module is a web module and is not Web.Foundation
        if($global:CurrentModuleName.StartsWith("Web", "CurrentCultureIgnoreCase") -and !$global:CurrentModuleName.EndsWith("Foundation", "CurrentCultureIgnoreCase")  -and !$global:CurrentModuleName.EndsWith("Presentation", "CurrentCultureIgnoreCase")){
            CompileLess
        } else {
            Write-Warning "Only web modules need this to be completed."
        }
    }
    
    if ($all) {
        CompileSMBAll
        CompileOTGAll
    }
    
    if ($smb) {
        CompileSMBAll
    }
    
    if ($otg) {
        CompileOTGAll
    }
}