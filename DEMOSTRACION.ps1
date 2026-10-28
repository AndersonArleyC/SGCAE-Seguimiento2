param([string]$BaseUrl = 'http://localhost:5300')
$ErrorActionPreference = 'Stop'
# Genera exclusivamente datos ficticios nuevos; no elimina ni altera datos previos.
function Enviar($Metodo, $Ruta, $Datos) {
    $opciones = @{ Method=$Metodo; Uri="$BaseUrl$Ruta" }
    if ($null -ne $Datos) { $opciones.ContentType='application/json; charset=utf-8'; $opciones.Body=($Datos | ConvertTo-Json -Depth 5) }
    Invoke-RestMethod @opciones
}
function Verificar($Condicion, $Mensaje) {
    if (-not $Condicion) { throw "FALLO: $Mensaje" }
    Write-Host "OK: $Mensaje" -ForegroundColor Green
}
$sufijo = [Guid]::NewGuid().ToString('N').Substring(0,12)
Enviar GET '/health/database' $null | Out-Host
$e = Enviar POST '/api/empleados' @{identificacion="DEMO-$sufijo";nombres='Ana';apellidos='Prueba';correo="demo-$sufijo@example.com";cargo='Auxiliar';area='Logistica'}
$p = Enviar POST '/api/puntos-acceso' @{nombre="Recepcion DEMO-$sufijo";habilitado=$true}
$c = Enviar POST '/api/credenciales' @{empleadoId=$e.id;codigo="TARJETA-$sufijo"}
$r = Enviar POST '/api/movimientos' @{codigoCredencial=$c.codigo;puntoAccesoId=$p.id;tipo='Entrada'}
Verificar ($r.resultado -eq 'Autorizado') 'Entrada autorizada'
$r = Enviar POST '/api/movimientos' @{codigoCredencial=$c.codigo;puntoAccesoId=$p.id;tipo='Salida'}
Verificar ($r.resultado -eq 'Autorizado') 'Salida autorizada'
Enviar PUT "/api/empleados/$($e.id)" @{nombres='Ana';apellidos='Prueba';correo=$e.correo;cargo='Supervisora';area='Logistica'} | Out-Null
$detalle = Enviar GET "/api/empleados/$($e.id)" $null
Verificar ($detalle.cargo -eq 'Supervisora') 'Actualizacion consultable'
Enviar PATCH "/api/credenciales/$($c.id)/bloquear" $null | Out-Null
$r = Enviar POST '/api/movimientos' @{codigoCredencial=$c.codigo;puntoAccesoId=$p.id;tipo='Entrada'}
Verificar ($r.resultado -eq 'Rechazado') 'Credencial bloqueada rechazada'
Enviar PATCH "/api/credenciales/$($c.id)/revocar" $null | Out-Null
$r = Enviar POST '/api/movimientos' @{codigoCredencial=$c.codigo;puntoAccesoId=$p.id;tipo='Entrada'}
Verificar ($r.resultado -eq 'Rechazado') 'Credencial revocada rechazada'
$c2 = Enviar POST '/api/credenciales' @{empleadoId=$e.id;codigo="SEGUNDA-$sufijo"}
Enviar PATCH "/api/puntos-acceso/$($p.id)/habilitacion" @{habilitado=$false} | Out-Null
$r = Enviar POST '/api/movimientos' @{codigoCredencial=$c2.codigo;puntoAccesoId=$p.id;tipo='Entrada'}
Verificar ($r.resultado -eq 'Rechazado') 'Punto deshabilitado rechazado'
Enviar PATCH "/api/puntos-acceso/$($p.id)/habilitacion" @{habilitado=$true} | Out-Null
Enviar PATCH "/api/empleados/$($e.id)/desactivar" $null | Out-Null
$r = Enviar POST '/api/movimientos' @{codigoCredencial=$c2.codigo;puntoAccesoId=$p.id;tipo='Entrada'}
Verificar ($r.resultado -eq 'Rechazado') 'Empleado inactivo rechazado'
$r = Enviar POST '/api/movimientos' @{codigoCredencial="INEXISTENTE-$sufijo";puntoAccesoId=$p.id;tipo='Entrada'}
Verificar ($r.resultado -eq 'Rechazado') 'Codigo inexistente auditado'
$historial = Enviar GET "/api/movimientos?empleadoId=$($e.id)&puntoAccesoId=$($p.id)&resultado=Rechazado&pagina=1&tamano=2" $null
Verificar ($historial.total -eq 4 -and $historial.items.Count -eq 2 -and $historial.totalPaginas -eq 2) 'Filtros y paginacion'
$pagina2 = Enviar GET "/api/movimientos?empleadoId=$($e.id)&resultado=Rechazado&pagina=2&tamano=2" $null
Verificar ($pagina2.items.Count -eq 2) 'Segunda pagina'
Write-Host "Empleado: $($e.id) / Punto: $($p.id)"
$historial | ConvertTo-Json -Depth 6
Write-Host 'Demostracion completada. Los registros DEMO quedan como evidencia en SQL Server.'
