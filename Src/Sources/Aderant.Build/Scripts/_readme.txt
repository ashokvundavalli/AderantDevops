You can use the provided batch scripts to persist this build.

PERSIST-BUILD.BAT - create labels/tags and retain the builds indefinitely
1. Provide a name (without spaces) that will be used for creating a TFS label and Git tags
2. Provide a comment for the label/tags
3. The script will attempt to do a dry run of all actions. If anything goes south here (e.g. due to missing rights or corrupt input file etc.), nothing will be actually happen
4. After a succesful dry run the script will run again, but this time apply the changes

UNDO-BUILDPERSISTENCE.BAT - remove labels/tags and revert retaining of the builds
1. Provide the same label/tag name you used in persist-build.bat to undo all if the changes you made
2. Dry run
3. Live run if the dry run succeeded


IMPORTANT: There will be a log file created (output will be appended) in C:\temp\persist-build-log.txt
