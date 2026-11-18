@echo off
rem ==========================================================================
rem  Verwijdert Examenopname van een laptop. De opnames zelf blijven bewaard.
rem  Afsluitcodes: 0 = gelukt, 1618 = er loopt een opname, later opnieuw.
rem ==========================================================================

if not exist "%SystemRoot%\Sysnative\cmd.exe" goto start
"%SystemRoot%\Sysnative\cmd.exe" /c ""%~f0" %*"
exit /b

:start
set "LOG=%ProgramData%\Examenopname-installatie.log"
echo ===== %date% %time% verwijderen >> "%LOG%"
call :verwijderen >> "%LOG%" 2>&1
set "CODE=%errorlevel%"
echo Afsluitcode: %CODE% >> "%LOG%"
exit /b %CODE%


:verwijderen
tasklist /fi "imagename eq Examenopname.exe" 2>nul | find /i "Examenopname.exe" >nul
if not errorlevel 1 (
    echo Er loopt een opname, later opnieuw proberen.
    exit /b 1618
)

schtasks /delete /tn "Examenopname\Waakhond" /f
schtasks /delete /tn "Examenopname\Opruimen" /f
del /f /q "%PUBLIC%\Desktop\Examenopname.lnk"
del /f /q "%ProgramData%\Microsoft\Windows\Start Menu\Programs\Examenopname*.lnk"
rmdir /s /q "%ProgramFiles%\Examenopname"
reg delete "HKLM\SOFTWARE\Examenopname" /f

echo Verwijderen klaar.
exit /b 0
