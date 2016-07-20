@echo off
SET setupRoot=%~dp0
powershell -noprofile -file %setupRoot:~0,-1%\Src\Profile\Aderant\Setup.ps1
pause