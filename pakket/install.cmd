@echo off
rem Intune draait als 32-bit proces, via Sysnative starten we de 64-bit PowerShell.
set PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe
if exist %SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe set PS=%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe
"%PS%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
exit /b %errorlevel%
