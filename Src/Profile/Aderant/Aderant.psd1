#
# Module manifest for module 'Aderant'
#
# Generated by: ADERANT
#
# Generated on: 21/04/2010
#

@{

# Script module or binary module file associated with this manifest
ModuleToProcess = ''

# Version number of this module.
ModuleVersion = '1.0'

# ID used to uniquely identify this module
GUID = 'f235a89f-3521-4f8d-8bec-dd3b660db514'

# Author of this module
Author = 'ADERANT'

# Company or vendor of this module
CompanyName = 'ADERANT'

# Copyright statement for this module
Copyright = 'ADERANT'

# Description of the functionality provided by this module
Description = 'Provides build functionality for Aderant Expert Modules'

# Minimum version of the Windows PowerShell engine required by this module
PowerShellVersion = ''

# Name of the Windows PowerShell host required by this module
PowerShellHostName = ''

# Minimum version of the Windows PowerShell host required by this module
PowerShellHostVersion = ''

# Minimum version of the .NET Framework required by this module
DotNetFrameworkVersion = '4.0'

# Minimum version of the common language runtime (CLR) required by this module
CLRVersion = '4.0'

# Processor architecture (None, X86, Amd64, IA64) required by this module
ProcessorArchitecture = ''

# Modules that must be imported into the global environment prior to importing this module
RequiredModules = @()

# Assemblies that must be loaded prior to importing this module
RequiredAssemblies = @('Aderant.Framework.Build.dll')

# Script files (.ps1) that are run in the caller's environment prior to importing this module
ScriptsToProcess = "ImportDependencyAnalyzer.ps1"

# Type files (.ps1xml) to be loaded when importing this module
TypesToProcess = @()

# Format files (.ps1xml) to be loaded when importing this module
FormatsToProcess = @()

# Modules to import as nested modules of the module specified in ModuleToProcess
NestedModules = 'Aderant.psm1', 'AderantTfs.psm1'

# Functions to export from this module
FunctionsToExport = '*'

# Cmdlets to export from this module
CmdletsToExport = '*'

# Variables to export from this module
VariablesToExport = '*'

# Aliases to export from this module
AliasesToExport = '*'

# List of all modules packaged with this module
ModuleList = @()

# List of all files packaged with this module
FileList = @()

# Private data to pass to the module specified in ModuleToProcess
PrivateData = ''

}

