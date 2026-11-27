# Publica una version nueva del cliente: pone la version, compila, firma y copia el programa al repositorio del servidor
# (..\server\public\download). Despues hay que subir los dos repositorios y actualizar el servidor.
#
#   powershell -ExecutionPolicy Bypass -File publicar.ps1
#
# La clave privada de firma esta en %USERPROFILE%\.bezares-chat\clave-firma.xml y NO debe copiarse al servidor ni a git.
# Haz una copia de seguridad en un lugar seguro: sin ella, los programas ya instalados no aceptaran mas actualizaciones.
param([string]$Server = (Join-Path $PSScriptRoot '..\server'))
$ErrorActionPreference = 'Stop'
$src = Join-Path $PSScriptRoot 'BezaresChat.cs'
$release = Join-Path $PSScriptRoot 'release\BezaresChat.exe'
$out = Join-Path $env:TEMP ('bzc-' + [guid]::NewGuid().ToString('N'))
$exe = Join-Path $out 'BezaresChat.exe'
$keyFile = Join-Path $env:USERPROFILE '.bezares-chat\clave-firma.xml'
$dest = Join-Path $Server 'public\download'
if (-not (Test-Path (Join-Path $Server 'public\index.php'))) { throw "No encuentro el repositorio del servidor en $Server" }
$utf8 = New-Object System.Text.UTF8Encoding($true)
$code = [IO.File]::ReadAllText($src, $utf8)

# --- clave de firma (se crea la primera vez)
$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider(3072)
$rsa.PersistKeyInCsp = $false
if (Test-Path $keyFile) {
    $rsa.FromXmlString([IO.File]::ReadAllText($keyFile))
} elseif ($code.Contains('"@@CLAVE_PUBLICA@@"')) {
    $dir = Split-Path $keyFile
    New-Item -ItemType Directory -Force $dir | Out-Null
    # Solo el usuario actual puede leer la carpeta.
    icacls $dir /inheritance:r /grant:r "$($env:USERDOMAIN)\$($env:USERNAME):(OI)(CI)F" | Out-Null
    [IO.File]::WriteAllText($keyFile, $rsa.ToXmlString($true))
} else {
    throw "Falta la clave de firma ($keyFile). Recuperala de la copia de seguridad."
}
$pub = $rsa.ToXmlString($false)
if ($code.Contains('"@@CLAVE_PUBLICA@@"')) { $code = $code.Replace('"@@CLAVE_PUBLICA@@"', '"' + $pub + '"') }
if (-not $code.Contains($pub)) { throw 'La clave de firma no corresponde a la clave publica del programa.' }

# --- version: anyo.mes.dia.numero
$now = Get-Date
$m = [regex]::Match($code, 'AssemblyVersion\("([\d.]+)"\)')
$cur = [version]$m.Groups[1].Value
$n = 1
if ($cur.Major -eq $now.Year -and $cur.Minor -eq $now.Month -and $cur.Build -eq $now.Day) { $n = $cur.Revision + 1 }
$ver = '{0}.{1}.{2}.{3}' -f $now.Year, $now.Month, $now.Day, $n
if ([version]$ver -le $cur) { throw "La version $ver no es posterior a la actual ($cur): revisa la fecha del equipo." }
$code = $code.Replace($m.Value, 'AssemblyVersion("' + $ver + '")')
[IO.File]::WriteAllText($src, $code, $utf8)

# --- compilar (en una carpeta temporal: el programa puede estar abierto desde release)
New-Item -ItemType Directory -Force $out | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
Push-Location $PSScriptRoot
try { & $csc /nologo /target:winexe /optimize+ /platform:anycpu "/out:$exe" /win32icon:icon.ico /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Security.dll BezaresChat.cs } finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { throw 'Error al compilar' }
# El antivirus puede tener ocupado el programa recien compilado durante unos segundos.
for ($i = 0; $i -lt 40; $i++) { try { [void][IO.File]::ReadAllBytes($exe); break } catch { Start-Sleep -Seconds 3 } }
if ([Reflection.AssemblyName]::GetAssemblyName($exe).Version.ToString() -ne $ver) { throw 'El programa compilado no tiene la version esperada' }

# --- firmar y copiar
$bytes = [IO.File]::ReadAllBytes($exe)
$sha = ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($bytes)) -replace '-', '').ToLowerInvariant()
$text = "BezaresChat|$ver|$($bytes.Length)|$sha"
$sig = [Convert]::ToBase64String($rsa.SignData([Text.Encoding]::UTF8.GetBytes($text), 'SHA256'))
New-Item -ItemType Directory -Force $dest | Out-Null
[IO.File]::WriteAllBytes((Join-Path $dest 'BezaresChat.exe'), $bytes)
$json = [ordered]@{ version = $ver; size = $bytes.Length; sha256 = $sha; sig = $sig } | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $dest 'version.json'), $json, (New-Object System.Text.UTF8Encoding($false)))
try { [IO.File]::WriteAllBytes($release, $bytes) } catch { Write-Host 'Aviso: release\BezaresChat.exe esta en uso y no se ha sustituido (se actualizara solo cuando se actualice el servidor).' }
Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue
Write-Host "Publicado: version $ver ($($bytes.Length) bytes)"
