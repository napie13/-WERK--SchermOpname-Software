@echo off
rem ==========================================================================
rem  Installeert Examenopname op een laptop.
rem
rem  Intune voert dit uit als SYSTEM. Zelf testen op een laptop kan ook:
rem  rechtsklik op install.cmd en kies "Als administrator uitvoeren".
rem
rem  Wat er gebeurt staat in %ProgramData%\Examenopname-installatie.log
rem  Afsluitcodes: 0 = gelukt, 1 = fout,
rem                1618 = er loopt een opname, Intune probeert later opnieuw.
rem ==========================================================================

rem Intune start installaties als 32-bit proces. Dan starten we dit script
rem opnieuw als 64-bit, anders komt alles in de verkeerde map terecht.
if not exist "%SystemRoot%\Sysnative\cmd.exe" goto start
"%SystemRoot%\Sysnative\cmd.exe" /c ""%~f0" %*"
exit /b

:start
set "BRON=%~dp0"
set "DOEL=%ProgramFiles%\Examenopname"
set "LOG=%ProgramData%\Examenopname-installatie.log"
echo ===== %date% %time% installatie >> "%LOG%"
call :installeren >> "%LOG%" 2>&1
set "CODE=%errorlevel%"
echo Afsluitcode: %CODE% >> "%LOG%"
exit /b %CODE%


:installeren
rem --- Versie en opnamemap uit instellingen.ini halen ---------------------
set "VERSIE="
set "OPNAMEMAP="
for /f "usebackq eol=; tokens=1,* delims==" %%a in ("%BRON%programma\instellingen.ini") do (
    if /i "%%a"=="Versie" set "VERSIE=%%b"
    if /i "%%a"=="OpnameMap" set "OPNAMEMAP=%%b"
)
if not defined VERSIE (
    echo FOUT: Versie niet gevonden in instellingen.ini
    exit /b 1
)
if not defined OPNAMEMAP (
    echo FOUT: OpnameMap niet gevonden in instellingen.ini
    exit /b 1
)
echo Versie %VERSIE%, opnamemap %OPNAMEMAP%

rem --- Nooit bijwerken terwijl er een opname loopt ----------------------
tasklist /fi "imagename eq Examenopname.exe" 2>nul | find /i "Examenopname.exe" >nul
if not errorlevel 1 (
    echo Er loopt een opname, later opnieuw proberen.
    exit /b 1618
)

rem --- Programma kopieren naar Program Files ----------------------------
echo Programma kopieren naar %DOEL%
robocopy "%BRON%programma" "%DOEL%" /mir /r:3 /w:5 /np /njh /njs
if errorlevel 8 exit /b 1

rem --- Opnamemap en rechten ---------------------------------------------
rem  SYSTEM en Administrators (S-1-5-18, S-1-5-32-544): volledige controle
rem  Users (S-1-5-32-545): enkel lezen en een eigen map aanmaken
rem  Maker en eigenaar (S-1-3-0, S-1-3-4): lezen en schrijven in hun eigen map,
rem  maar niets verwijderen en geen rechten aanpassen
echo Opnamemap instellen
if not exist "%OPNAMEMAP%" mkdir "%OPNAMEMAP%"
icacls "%OPNAMEMAP%" /reset
icacls "%OPNAMEMAP%" /setowner *S-1-5-32-544
icacls "%OPNAMEMAP%" /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F" "*S-1-5-32-545:(RX,AD)" "*S-1-3-0:(OI)(CI)(IO)(RX,W)" "*S-1-3-4:(OI)(CI)(RX,W)"
if errorlevel 1 exit /b 1

rem --- Geplande taken ---------------------------------------------------
rem  Waakhond: bij elke aanmelding en daarna elke minuut, als de gebruiker.
rem  Opruimen: bij het opstarten van de laptop, als SYSTEM.
echo Geplande taken aanmaken
schtasks /create /tn "Examenopname\Waakhond" /xml "%BRON%waakhond.xml" /f
if errorlevel 1 exit /b 1
schtasks /create /tn "Examenopname\Opruimen" /tr "\"%DOEL%\Examenopname.exe\" /opruimen" /sc onstart /ru SYSTEM /f
if errorlevel 1 exit /b 1

rem --- Snelkoppelingen --------------------------------------------------
echo Snelkoppelingen plaatsen
copy /y "%BRON%snelkoppelingen\Examenopname.lnk" "%PUBLIC%\Desktop\"
copy /y "%BRON%snelkoppelingen\*.lnk" "%ProgramData%\Microsoft\Windows\Start Menu\Programs\"

rem --- Versie in het register, hierop controleert Intune ----------------
reg add "HKLM\SOFTWARE\Examenopname" /v Versie /t REG_SZ /d "%VERSIE%" /f
if errorlevel 1 exit /b 1

echo Installatie gelukt.
exit /b 0
