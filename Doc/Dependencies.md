# Getting Dependencies
Currently, all of our external dependencies come in the form of NuGet packages, and are retrieved from one of two sources:
* https://expertpackages.azurewebsites.net/ for packages published internally.
* https://www.nuget.org/ for public packages.

We use a third party application called Paket to manage packages. Documentation on this tool can be found here: https://fsprojects.github.io/Paket/

The command included in the Aderant PowerShell profile to interact with this tool is called: ```Get-Dependencies```, and has the alias ```gd```.

```Get-Dependencies``` runs [paket restore](https://fsprojects.github.io/Paket/paket-restore.html).

```Get-Dependencies -Force``` runs [paket update](https://fsprojects.github.io/Paket/paket-update.html).

The behaviour of the ```Get-Dependencies``` (```gd```) PowerShell command should be consistent between both the ExpertSuite repository, as well as other repositories.

This command included symlink support when running in repositories with a shared dependency directory configured.

## Switches
### -NoSymlinks
This switch is intended to be used to avoid using a shared dependency directory if it is configured in the Build\BranchConfig.xml file of a repository. All packages will be confined to the working directory.
### -Force
This switch will run Paket in update mode, which will remove the paket.lock file if it exists.

## The ExpertSuite Experience™
* ```gd``` in root - scans all subdirectories with a search depth of 1 for paket.dependencies files and runs paket restore.
* ```gd``` in module - retrieves packages for the given module, as well as well as existing packages in the shared dependency directory. Symlinks will be created to the shared dependency directory.
* ```gd -NoSymlinks``` in module - retrieves packages for the specific module. All packages will be confined to the working directory and the shared dependency directory will not be used.

## Other Repositories
* ```gd``` - runs Paket restore.
* ```gd -Force``` - runs Paket update to update the paket.lock file.