# Common-Build-Project #

A project hook is installed into all .csproj files during the build. This hook is used to dynamically install the Rosyln code analyzer. Other extensions are planned for the future.
If your project does not have the hook in place it will be added during the build process and a warning will be generated. It is recommended that you commit the .csproj modifications to avoid the warning on each build.

## How does it work? ##

For all .csproj files we inject a special import into the project itself which scans up from the current file to the root drive looking for "dir.proj".

```xml
<CommonBuildProject>$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), 'dir.proj'))</CommonBuildProject>
```

During ```gd``` a default dir.proj file is deployed to the module directory which will be found and loaded. You can place a dir.proj of your own anywhere in your project structure to override the defaults provided by this default file.

