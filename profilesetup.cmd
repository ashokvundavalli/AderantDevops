@echo off
SET setupRoot=%~dp0
powershell -noprofile -file %setupRoot:~0,-1%\Src\Profile\Aderant\Setup.ps1

git config --global credential.tfs.integrated true
git config --global core.autocrlf false

pause