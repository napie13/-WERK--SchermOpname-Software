# =============================================================================
#  Bouwt het Intune-pakket van Examenopname. Start dit via Maak.cmd.
#
#    1. Instellingen overlopen en bewaren in instellingen.ini
#    2. Het leerkrachtpaswoord vragen (alleen een hash gaat mee in het pakket)
#    3. FFmpeg en de Intune-tool ophalen als ze nog niet in bouw\hulpmiddelen staan
#    4. Het programma compileren met de C#-compiler die in Windows zit
#    5. Het pakket samenstellen in uitvoer\pakket
#    6. Er een .intunewin van maken in uitvoer\
#
#  Er wordt niets geïnstalleerd op deze computer.
# =============================================================================

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # downloads gaan veel sneller zonder voortgangsbalk
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$root         = Split-Path $PSScriptRoot -Parent
$instellingen = Join-Path $root 'instellingen.ini'
$hulp         = Join-Path $PSScriptRoot 'hulpmiddelen'
$uitvoer      = Join-Path $root 'uitvoer'
$pakket       = Join-Path $uitvoer 'pakket'
$installMap   = 'C:\Program Files\Examenopname'   # hier zet install.cmd het programma op de laptop

$ffmpegUrl = 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip'
$intuneUrl = 'https://github.com/microsoft/Microsoft-Win32-Content-Prep-Tool/raw/master/IntuneWinAppUtil.exe'

function Stap($tekst) {
    Write-Host ''
    Write-Host "=== $tekst" -ForegroundColor Cyan
}

function LeesPaswoord($vraag) {
    $veilig = Read-Host $vraag -AsSecureString
    return (New-Object Net.NetworkCredential('', $veilig)).Password
}

function Download($url, $bestand) {
    try {
        Invoke-WebRequest $url -OutFile $bestand -UseBasicParsing
    }
    catch {
        throw "Download mislukt: $url`nZet het bestand zelf in bouw\hulpmiddelen, zie docs\Technische-handleiding.md."
    }
}

try {
    # --- 1. Instellingen --------------------------------------------------------
    Stap 'Instellingen overlopen (Enter = huidige waarde behouden)'
    $regels = [IO.File]::ReadAllLines($instellingen)
    for ($i = 0; $i -lt $regels.Length; $i++) {
        $regel = $regels[$i]
        if ($regel.TrimStart().StartsWith(';')) {
            Write-Host $regel.TrimStart(';', ' ') -ForegroundColor DarkGray   # de uitleg uit het bestand tonen
            continue
        }
        $pos = $regel.IndexOf('=')
        if ($pos -lt 1) { continue }

        $naam = $regel.Substring(0, $pos).Trim()
        $huidig = $regel.Substring($pos + 1).Trim()
        $antwoord = (Read-Host "  $naam [$huidig]").Trim()
        if ($antwoord -eq '') { continue }
        if ($huidig -match '^\d+$' -and $antwoord -notmatch '^\d+$') {
            Write-Host '  Geef een getal.' -ForegroundColor Yellow
            $i--   # dezelfde vraag nog eens
            continue
        }
        $regels[$i] = "$naam=$antwoord"
    }
    [IO.File]::WriteAllLines($instellingen, $regels)
    $versie = ($regels | Where-Object { $_ -like 'Versie=*' }) -replace '^Versie=', ''

    # --- 2. Paswoord ------------------------------------------------------------
    Stap 'Paswoord voor de leerkrachten'
    Write-Host 'Leerkrachten hebben dit paswoord nodig om een opname te stoppen. Minstens 8 tekens.'
    while ($true) {
        $paswoord = LeesPaswoord '  Paswoord'
        $herhaling = LeesPaswoord '  Nog eens'
        if ($paswoord -cne $herhaling) { Write-Host '  De twee paswoorden zijn niet gelijk.' -ForegroundColor Yellow }
        elseif ($paswoord.Length -lt 8) { Write-Host '  Te kort.' -ForegroundColor Yellow }
        else { break }
    }
    # PBKDF2-SHA256 met 100000 rondes en een willekeurige salt. Instellingen.cs controleert op dezelfde manier.
    $salt = New-Object byte[] 16
    (New-Object Security.Cryptography.RNGCryptoServiceProvider).GetBytes($salt)
    $kdf = New-Object Security.Cryptography.Rfc2898DeriveBytes($paswoord, $salt, 100000, [Security.Cryptography.HashAlgorithmName]::SHA256)
    $hash = 'pbkdf2-sha256$100000$' + [Convert]::ToBase64String($salt) + '$' + [Convert]::ToBase64String($kdf.GetBytes(32))

    # --- 3. Hulpmiddelen --------------------------------------------------------
    Stap 'Hulpmiddelen controleren'
    $ffmpeg = Join-Path $hulp 'ffmpeg.exe'
    if (Test-Path $ffmpeg) {
        Write-Host '  FFmpeg is aanwezig.'
    }
    else {
        Write-Host '  FFmpeg downloaden van gyan.dev (ongeveer 100 MB)...'
        $zip = Join-Path $env:TEMP 'ffmpeg-essentials.zip'
        Download $ffmpegUrl $zip
        Download "$ffmpegUrl.sha256" "$zip.sha256"
        $verwacht = [IO.File]::ReadAllText("$zip.sha256").Trim().Split(' ')[0]
        if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne $verwacht) {
            throw 'De download van FFmpeg is beschadigd (het controlegetal klopt niet). Probeer opnieuw.'
        }
        $uitgepakt = Join-Path $env:TEMP 'ffmpeg-essentials'
        if (Test-Path $uitgepakt) { Remove-Item $uitgepakt -Recurse -Force }
        Expand-Archive $zip $uitgepakt
        $exe = Get-ChildItem $uitgepakt -Recurse -Filter 'ffmpeg.exe' | Select-Object -First 1
        Copy-Item $exe.FullName $ffmpeg
        $licentie = Join-Path $exe.Directory.Parent.FullName 'LICENSE'
        if (Test-Path $licentie) { Copy-Item $licentie (Join-Path $hulp 'ffmpeg-licentie.txt') }
    }

    $intune = Join-Path $hulp 'IntuneWinAppUtil.exe'
    if (Test-Path $intune) {
        Write-Host '  Intune-tool is aanwezig.'
    }
    else {
        Write-Host '  Intune-tool van Microsoft downloaden...'
        Download $intuneUrl $intune
    }
    if ((Get-AuthenticodeSignature $intune).Status -ne 'Valid') {
        Write-Host '  Let op: IntuneWinAppUtil.exe heeft geen geldige digitale handtekening.' -ForegroundColor Yellow
    }

    # --- 4. Compileren ----------------------------------------------------------
    Stap 'Programma compileren'
    if (Test-Path $uitvoer) { Remove-Item $uitvoer -Recurse -Force }
    $programma = Join-Path $pakket 'programma'
    New-Item $programma, (Join-Path $pakket 'snelkoppelingen') -ItemType Directory -Force | Out-Null

    # De C#-compiler van .NET Framework 4 staat op elke Windows 10 en 11.
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
    & $csc /nologo /optimize+ /codepage:65001 /target:winexe `
        "/win32icon:$root\programma\logo.ico" `
        "/out:$programma\Examenopname.exe" `
        /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
        "$root\programma\*.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Compileren mislukt, zie de fouten hierboven.' }

    # --- 5. Pakket samenstellen -------------------------------------------------
    Stap 'Pakket samenstellen'
    Copy-Item $ffmpeg, "$root\programma\logo.png", "$root\docs\Handleiding.html" $programma
    if (Test-Path "$hulp\ffmpeg-licentie.txt") { Copy-Item "$hulp\ffmpeg-licentie.txt" $programma }

    # Dezelfde instellingen als hierboven, plus de paswoordhash.
    [IO.File]::WriteAllLines("$programma\instellingen.ini", [string[]]($regels + "PaswoordHash=$hash"))

    Copy-Item "$root\installatie\install.cmd", "$root\installatie\uninstall.cmd" $pakket
    # Taakplanner verwacht het XML-bestand in UTF-16.
    $xml = [IO.File]::ReadAllText("$root\installatie\waakhond.xml")
    [IO.File]::WriteAllText("$pakket\waakhond.xml", $xml, [Text.Encoding]::Unicode)

    # Snelkoppelingen. Ze verwijzen naar de plek waar install.cmd het programma zet.
    $shell = New-Object -ComObject WScript.Shell
    $lnk = $shell.CreateShortcut("$pakket\snelkoppelingen\Examenopname.lnk")
    $lnk.TargetPath = "$installMap\Examenopname.exe"
    $lnk.Save()
    $lnk = $shell.CreateShortcut("$pakket\snelkoppelingen\Examenopname handleiding.lnk")
    $lnk.TargetPath = "$installMap\Handleiding.html"
    $lnk.Save()

    # --- 6. Intune-pakket -------------------------------------------------------
    Stap 'Intune-pakket maken'
    & $intune -c $pakket -s install.cmd -o $uitvoer -q
    if (-not (Test-Path "$uitvoer\install.intunewin")) { throw 'Het Intune-pakket werd niet gemaakt, zie de meldingen hierboven.' }
    $naam = "Examenopname-$versie.intunewin"
    Rename-Item "$uitvoer\install.intunewin" $naam

    Stap 'Klaar'
    Write-Host "  Pakket:      uitvoer\$naam" -ForegroundColor Green
    Write-Host '  Installeren: install.cmd     Verwijderen: uninstall.cmd'
    Write-Host "  Detectie:    register HKLM\SOFTWARE\Examenopname, waarde Versie = $versie"
    Start-Process explorer.exe $uitvoer
}
catch {
    Write-Host ''
    Write-Host "FOUT: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
