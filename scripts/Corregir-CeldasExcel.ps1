<#
.SYNOPSIS
Corrige dos rangos en todos los archivos de Excel encontrados de forma recursiva.

.DESCRIPTION
Busca desde la carpeta donde se encuentra el script (o desde -RutaBase) y escribe
0992424362001 como texto en:
  - Hoja 1: Q5:Y5
  - Hoja 4: L4:Q4

Genera un informe CSV al finalizar.

.EXAMPLE
.\Corregir-CeldasExcel.ps1

.EXAMPLE
.\Corregir-CeldasExcel.ps1 -RutaBase "D:\Carpeta principal" -SinConfirmacion
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$RutaBase,

    [Parameter()]
    [switch]$SinConfirmacion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RutaBase)) {
    $RutaBase = $PSScriptRoot
}

$valorCorrecto = '0992424362001'
$extensionesCompatibles = @('.xlsx', '.xlsm', '.xlsb', '.xls', '.xltx', '.xltm')
$marcaDeTiempo = Get-Date -Format 'yyyyMMdd_HHmmss'

function Liberar-ObjetoCom {
    param([object]$Objeto)

    if ($null -ne $Objeto -and [System.Runtime.InteropServices.Marshal]::IsComObject($Objeto)) {
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($Objeto)
    }
}

function Establecer-TextoEnRango {
    param(
        [Parameter(Mandatory)]
        [object]$Hoja,

        [Parameter(Mandatory)]
        [string]$Direccion,

        [Parameter(Mandatory)]
        [string]$Valor
    )

    $rangoSolicitado = $null
    $rangoCombinado = $null
    $celdas = $null
    $celdaInicial = $null
    $celdaDestino = $null

    try {
        $rangoSolicitado = $Hoja.Range($Direccion)
        $celdas = $rangoSolicitado.Cells
        $celdaInicial = $celdas.Item(1, 1)

        # Excel solo admite MergeArea de forma fiable desde una unica celda.
        if ([bool]$celdaInicial.MergeCells) {
            $rangoCombinado = $celdaInicial.MergeArea
            $rangoCombinado.ClearContents()

            Liberar-ObjetoCom $celdas
            $celdas = $null
            $celdas = $rangoCombinado.Cells
        }
        else {
            $rangoSolicitado.ClearContents()
        }

        $celdaDestino = $celdas.Item(1, 1)
        $celdaDestino.NumberFormat = '@'
        $celdaDestino.Value2 = $Valor
    }
    finally {
        Liberar-ObjetoCom $celdaDestino
        Liberar-ObjetoCom $celdaInicial
        Liberar-ObjetoCom $celdas
        Liberar-ObjetoCom $rangoCombinado
        Liberar-ObjetoCom $rangoSolicitado
    }
}

try {
    $rutaResuelta = (Resolve-Path -LiteralPath $RutaBase).Path
}
catch {
    Write-Error "La ruta base no existe o no se puede abrir: $RutaBase"
    exit 1
}

$archivos = @(
    Get-ChildItem -LiteralPath $rutaResuelta -File -Recurse |
        Where-Object {
            $_.Name -notlike '~$*' -and
            $extensionesCompatibles -contains $_.Extension.ToLowerInvariant()
        } |
        Sort-Object FullName
)

if ($archivos.Count -eq 0) {
    Write-Host "No se encontraron archivos de Excel en '$rutaResuelta' ni en sus subdirectorios."
    exit 0
}

Write-Host "Se encontraron $($archivos.Count) archivo(s) de Excel."
Write-Host "Se reemplazara el contenido de Hoja 1 Q5:Y5 y Hoja 4 L4:Q4 por '$valorCorrecto'."

if (-not $SinConfirmacion) {
    $respuesta = Read-Host "Escriba SI para continuar"
    if ($respuesta.Trim().ToUpperInvariant() -ne 'SI') {
        Write-Host 'Operacion cancelada. No se modifico ningun archivo.'
        exit 0
    }
}

$excel = $null
$libros = $null
$resultados = [System.Collections.Generic.List[object]]::new()

try {
    try {
        $excel = New-Object -ComObject Excel.Application
    }
    catch {
        throw 'No se pudo iniciar Microsoft Excel. Este script debe ejecutarse en una computadora con Microsoft Excel instalado.'
    }

    $excel.Visible = $false
    $excel.DisplayAlerts = $false
    $excel.AskToUpdateLinks = $false
    $excel.EnableEvents = $false

    # Fuerza la desactivacion de macros al abrir archivos mediante automatizacion.
    $excel.AutomationSecurity = 3
    $libros = $excel.Workbooks

    foreach ($archivo in $archivos) {
        $libro = $null
        $hojas = $null
        $hoja1 = $null
        $hoja4 = $null
        $etapaActual = 'Preparando archivo'

        try {
            $etapaActual = 'Abriendo libro'
            $libro = $libros.Open($archivo.FullName, 0, $false)

            if ([bool]$libro.ReadOnly) {
                throw 'Excel abrio el archivo en modo de solo lectura.'
            }

            $etapaActual = 'Comprobando hojas'
            $hojas = $libro.Worksheets
            if ([int]$hojas.Count -lt 4) {
                throw "El libro solo tiene $($hojas.Count) hoja(s); se necesitan al menos 4."
            }

            $hoja1 = $hojas.Item(1)
            $hoja4 = $hojas.Item(4)

            $etapaActual = 'Actualizando Hoja 1 Q5:Y5'
            Establecer-TextoEnRango -Hoja $hoja1 -Direccion 'Q5:Y5' -Valor $valorCorrecto

            $etapaActual = 'Actualizando Hoja 4 L4:Q4'
            Establecer-TextoEnRango -Hoja $hoja4 -Direccion 'L4:Q4' -Valor $valorCorrecto

            $etapaActual = 'Guardando libro'
            $libro.Save()

            $resultados.Add([pscustomobject]@{
                    Archivo = $archivo.FullName
                    Estado  = 'CORREGIDO'
                    Detalle = 'Hoja 1 Q5:Y5 y Hoja 4 L4:Q4 actualizados.'
                })

            Write-Host "[CORREGIDO] $($archivo.FullName)" -ForegroundColor Green
        }
        catch {
            $detalleError = "$etapaActual`: $($_.Exception.Message)"

            $resultados.Add([pscustomobject]@{
                    Archivo = $archivo.FullName
                    Estado  = 'ERROR'
                    Detalle = $detalleError
                })

            Write-Warning "[ERROR] $($archivo.FullName): $detalleError"
        }
        finally {
            if ($null -ne $libro) {
                try {
                    $libro.Close($false)
                }
                catch {
                    Write-Warning "No se pudo cerrar correctamente '$($archivo.FullName)': $($_.Exception.Message)"
                }
            }

            Liberar-ObjetoCom $hoja4
            Liberar-ObjetoCom $hoja1
            Liberar-ObjetoCom $hojas
            Liberar-ObjetoCom $libro
        }
    }
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
finally {
    Liberar-ObjetoCom $libros

    if ($null -ne $excel) {
        try {
            $excel.Quit()
        }
        catch {
            Write-Warning "No se pudo cerrar Microsoft Excel correctamente: $($_.Exception.Message)"
        }
    }

    Liberar-ObjetoCom $excel
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

$rutaInforme = Join-Path $rutaResuelta "correccion_excel_$marcaDeTiempo.csv"
$resultados | Export-Csv -LiteralPath $rutaInforme -NoTypeInformation -Encoding UTF8

$corregidos = @($resultados | Where-Object Estado -eq 'CORREGIDO').Count
$errores = @($resultados | Where-Object Estado -eq 'ERROR').Count

Write-Host ''
Write-Host "Proceso terminado: $corregidos corregido(s), $errores error(es)."
Write-Host "Informe: $rutaInforme"

if ($errores -gt 0) {
    exit 1
}

exit 0
