@echo off
cls
echo You are running %~nx0 from %~dp0. Current dir is %cd%
PowerShell -NoProfile -ExecutionPolicy Bypass -Command "%~dp0\persist-build.ps1"
pause