# Installeert of verwijdert Examenopname. Intune start dit als SYSTEM via install.cmd of uninstall.cmd.
param([switch]$Verwijderen)

$ErrorActionPreference = 'Stop'
$versie = '1.0.0'
$doel = Join-Path $env:ProgramFiles 'Examenopname'
$exe = Join-Path $doel 'Examenopname.exe'
$snelkoppelingen = "$env:PUBLIC\Desktop\Examenopname.lnk", "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Examenopname.lnk"

Start-Transcript "$env:ProgramData\Examenopname-installatie.log" -Append | Out-Null

# Nooit bijwerken of verwijderen tijdens een opname. Met code 1618 probeert Intune het later opnieuw.
if (Get-Process Examenopname -ErrorAction SilentlyContinue) { Stop-Transcript; exit 1618 }

Unregister-ScheduledTask -TaskName 'Examenopname waakhond', 'Examenopname opruimen' -Confirm:$false -ErrorAction SilentlyContinue

if ($Verwijderen) {
    # De opnames zelf blijven staan, die kunnen nog nodig zijn.
    Remove-Item $snelkoppelingen, $doel, 'HKLM:\SOFTWARE\Examenopname' -Recurse -Force -ErrorAction SilentlyContinue
    Stop-Transcript
    exit 0
}

New-Item $doel -ItemType Directory -Force | Out-Null
Copy-Item "$PSScriptRoot\*" $doel -Exclude '*.cmd', 'install.ps1' -Force

# Opnamemap: leerlingen mogen een eigen map aanmaken en erin schrijven, maar niets verwijderen,
# geen rechten aanpassen en niet in de map van een andere leerling kijken.
#   SY en BA: volledige controle, BU: map aanmaken in de hoofdmap,
#   CO (maker) en OW (eigenaar): lezen en schrijven, zonder verwijderen of rechten wijzigen.
$map = ([regex]'(?m)^\s*OpnameMap\s*=\s*(.+?)\s*$').Match((Get-Content "$doel\instellingen.ini" -Raw)).Groups[1].Value
if (-not $map) { throw 'OpnameMap ontbreekt in instellingen.ini' }
New-Item $map -ItemType Directory -Force | Out-Null
$acl = Get-Acl $map
$acl.SetSecurityDescriptorSddlForm('O:BAD:PAI(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;;0x1200ad;;;BU)(A;OICIIO;0x1201bf;;;CO)(A;OICI;0x1201bf;;;OW)')
Set-Acl $map $acl

function Taak($naam, $trigger, $principal, $argument) {
    Register-ScheduledTask -TaskName $naam -Force -Xml @"
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <Triggers>$trigger</Triggers>
  <Principals><Principal id="Author">$principal</Principal></Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <StartWhenAvailable>true</StartWhenAvailable>
    <ExecutionTimeLimit>PT30M</ExecutionTimeLimit>
    <Hidden>true</Hidden>
  </Settings>
  <Actions Context="Author"><Exec><Command>"$exe"</Command><Arguments>$argument</Arguments></Exec></Actions>
</Task>
"@ | Out-Null
}

# Waakhond: elke minuut, bij elke aangemelde gebruiker. Start de opname opnieuw als die wegviel.
Taak 'Examenopname waakhond' `
    '<LogonTrigger><Repetition><Interval>PT1M</Interval></Repetition></LogonTrigger>' `
    '<GroupId>S-1-5-32-545</GroupId><RunLevel>LeastPrivilege</RunLevel>' '/waakhond'

# Opruimen: dagelijks als SYSTEM, verwijdert opnames ouder dan BewaarDagen.
Taak 'Examenopname opruimen' `
    '<CalendarTrigger><StartBoundary>2026-01-01T12:30:00</StartBoundary><ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay></CalendarTrigger>' `
    '<UserId>S-1-5-18</UserId><RunLevel>HighestAvailable</RunLevel>' '/opruimen'

$shell = New-Object -ComObject WScript.Shell
foreach ($pad in $snelkoppelingen) {
    $lnk = $shell.CreateShortcut($pad)
    $lnk.TargetPath = $exe
    $lnk.Save()
}

# Hierop controleert Intune of de installatie gelukt is.
New-Item 'HKLM:\SOFTWARE\Examenopname' -Force | New-ItemProperty -Name Versie -Value $versie -Force | Out-Null
Stop-Transcript
