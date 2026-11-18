@echo off
rem Dubbelklik om het Intune-pakket van Examenopname te bouwen.
rem Het eigenlijke werk gebeurt in bouw\maak.ps1.
title Examenopname - pakket maken
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0bouw\maak.ps1"
echo.
pause
