# ============================================================================
# Limpiar_Fotos_LOG.ps1
# Guillermo Carrillo - Linea 2 TLA
# Borra las fotos .bmp de D:\EVMS\TP\LOG que tengan mas de $HorasAGuardar horas.
# Las recientes se quedan (por si Calidad necesita revisar una pieza) y nunca
# toca un archivo que el programa este escribiendo en ese momento.
# Solo borra *.bmp de esa carpeta: no entra a subcarpetas ni toca .txt/.csv.
#
# PROGRAMAR CADA 3 HORAS (CMD como administrador, una sola vez):
#   1) Copiar este archivo a C:\Scripts\Limpiar_Fotos_LOG.ps1
#   2) Correr:
#   schtasks /Create /TN "Limpiar fotos LOG" /TR "powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\Scripts\Limpiar_Fotos_LOG.ps1" /SC HOURLY /MO 3 /RU SYSTEM /RL HIGHEST /F
#
# PROBAR A MANO (no borra nada, solo cuenta):
#   powershell -NoProfile -ExecutionPolicy Bypass -File C:\Scripts\Limpiar_Fotos_LOG.ps1 -Prueba
# ============================================================================

param(
    [switch]$Prueba
)

# ---- Ajustes ---------------------------------------------------------------
$Carpeta        = "D:\EVMS\TP\LOG"
$Extension      = "*.bmp"
$HorasAGuardar  = 24          # fotos mas nuevas que esto NO se borran
$Bitacora       = "C:\Scripts\Limpiar_Fotos_LOG.txt"
$MaxLineasBitacora = 2000     # para que la bitacora tampoco crezca sin fin
# ----------------------------------------------------------------------------

function Escribir($texto) {
    $linea = (Get-Date -Format "yyyy-MM-dd HH:mm:ss") + "  " + $texto
    try {
        Add-Content -Path $Bitacora -Value $linea
    } catch { }
    Write-Host $linea
}

if (-not (Test-Path $Carpeta)) {
    Escribir "No existe $Carpeta. No se hizo nada."
    exit 1
}

$limite   = (Get-Date).AddHours(-$HorasAGuardar)
$borrados = 0
$fallidos = 0
$bytes    = 0

Get-ChildItem -Path $Carpeta -Filter $Extension -ErrorAction SilentlyContinue |
    Where-Object { -not $_.PSIsContainer -and $_.LastWriteTime -lt $limite } |
    ForEach-Object {
        if ($Prueba) {
            $borrados++
            $bytes += $_.Length
        } else {
            try {
                $tam = $_.Length
                Remove-Item -LiteralPath $_.FullName -Force -ErrorAction Stop
                $borrados++
                $bytes += $tam
            } catch {
                $fallidos++    # en uso o sin permiso: se intenta en la siguiente corrida
            }
        }
    }

$mb = [math]::Round($bytes / 1MB, 1)
if ($Prueba) {
    Escribir "PRUEBA: se borrarian $borrados fotos ($mb MB) con mas de $HorasAGuardar h."
} else {
    Escribir "Borradas $borrados fotos ($mb MB) con mas de $HorasAGuardar h. Fallaron: $fallidos."
}

# Recortar la bitacora
try {
    $lineas = Get-Content $Bitacora -ErrorAction Stop
    if ($lineas.Count -gt $MaxLineasBitacora) {
        $lineas | Select-Object -Last $MaxLineasBitacora | Set-Content $Bitacora
    }
} catch { }
