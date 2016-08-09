@echo off
SET setupRoot=%~dp0
powershell -noprofile -file %setupRoot:~0,-1%\Src\Profile\Aderant\Setup.ps1

pushd %USERPROFILE%
set HOME=%USERPROFILE%
git.exe config --global credential.tfs.integrated true
git.exe config --global core.autocrlf false
popd

pause