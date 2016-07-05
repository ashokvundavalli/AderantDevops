#
# Module manifest for module "Aderant"
#
# Generated by: ADERANT
#
# Generated on: 21/04/2010
#

@{
    # ID used to uniquely identify this module
    GUID = "f235a89f-3521-4f8d-8bec-dd3b660db514"

    Author = "Aderant"
    CompanyName = "Aderant"
    Copyright = "Aderant"
    Description = "Provides build functionality for Aderant Expert Modules"

    PowerShellVersion = "4.0"

    CLRVersion = "4.0"

    # Version number of this module.
    ModuleVersion = "2.1"

    RootModule = "Aderant.psm1"    

    # Script files (.ps1) that are run in the caller"s environment prior to importing this module
    ScriptsToProcess = "BuildLibraryImport.ps1"

    NestedModules = @('AderantTfs.psm1', 'AderantWeb.psm1')

    # Type files (.ps1xml) to be loaded when importing this module
    TypesToProcess = @()    

    # Functions to export from this module
    FunctionsToExport = "*"

    # Cmdlets to export from this module
    CmdletsToExport = "*"

    # Variables to export from this module
    VariablesToExport = "*"

    # Aliases to export from this module
    AliasesToExport = "*"

    # List of all modules packaged with this module
    ModuleList = @()

    # List of all files packaged with this module
    FileList = @()

    # Private data to pass to the module specified in ModuleToProcess
    PrivateData = ""
}

