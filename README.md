# SGCAE - Seguimiento 2

Backend académico de gestión y control de acceso de empleados, implementado en .NET 10 / ASP.NET Core, Clean Architecture, DDD, CQRS y EF Core con SQL Server. No modifica los proyectos Biblioteca ni DistributedRealEstate.

## 1. Abrir

Abra `SGCAE.sln` con Visual Studio 2026 (no Visual Studio Code si desea usar el menú Compilar). En PowerShell, sin administrador:

```powershell
cd "C:\Users\Ander\Documents\Codex\2026-08-16\estoy-en-una-clase-de-programaci\outputs\SGCAE"
dotnet build
dotnet test
```

Requiere SDK .NET 10, SQL Server local en ejecución y permisos de su cuenta Windows para crear/usar una base nueva llamada SGCAE. Si su instancia es SQLEXPRESS cambie `Server=localhost` por `Server=localhost\SQLEXPRESS`.

## 2. Conectar y crear la base (primera vez)

```powershell
$env:ConnectionStrings__Accesos = "Server=localhost;Database=SGCAE;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
dotnet run --project ".\src\SGCAE.API" -- --initialize-database
```

`TrustServerCertificate=True` omite verificar el certificado: exclusivamente para esta práctica local, nunca producción. No guarda credenciales SQL en Git. Se usa EnsureCreated para una base nueva y exclusiva; no elimina bases ni actualiza esquemas existentes. No mezclar este mecanismo con migraciones de EF. Si cambia el modelo, prepare una base nueva con otro nombre o implemente migraciones con una estrategia de actualización; no borre datos como solución automática.

## 3. Ejecutar

En la misma PowerShell:

```powershell
dotnet run --project ".\src\SGCAE.API" -- --urls http://localhost:5300
```

Deje abierta esa ventana. Abra http://localhost:5300 en Chrome/Edge. Es una página de documentación, no una interfaz de administración. `/health` comprueba el proceso; `/health/database` comprueba SQL Server. Detenga con Ctrl+C.

## 4. Demostrar el funcionamiento

Con el servidor ejecutándose, abra OTRA PowerShell:

```powershell
cd "C:\Users\Ander\Documents\Codex\2026-08-16\estoy-en-una-clase-de-programaci\outputs\SGCAE"
& .\DEMOSTRACION.ps1
```

No cambie la política de ejecución del equipo si Windows bloquea scripts: pruebe las peticiones de `SGCAE.http` desde Visual Studio o Postman. La demostración escribe datos ficticios con identificadores nuevos y los conserva como evidencia. No elimina datos. No es una prueba aislada: úsela solamente contra su base académica local.

## API REST

Todos los cuerpos son JSON. Los enums se envían como texto (`Entrada`, `Salida`, `Autorizado`, `Rechazado`). Identificadores UUID/GUID. Fechas ISO 8601 con zona, preferiblemente UTC `Z`. Ejemplos completos en `SGCAE.http`.

| Método | Ruta | Función / cuerpo |
|---|---|---|
| POST | /api/empleados | identificacion, nombres, apellidos, correo, cargo, area |
| GET | /api/empleados | pagina=1, tamano=20, activo opcional |
| GET | /api/empleados/{id} | Detalle |
| PUT | /api/empleados/{id} | nombres, apellidos, correo, cargo, area |
| PATCH | /api/empleados/{id}/desactivar | Sin cuerpo |
| GET | /api/empleados/{id}/credenciales | Credenciales del empleado |
| POST | /api/credenciales | empleadoId, codigo |
| GET | /api/credenciales/{id} | Estado |
| PATCH | /api/credenciales/{id}/bloquear | Sin cuerpo |
| PATCH | /api/credenciales/{id}/revocar | Sin cuerpo |
| POST | /api/puntos-acceso | nombre, habilitado |
| GET | /api/puntos-acceso | Listado |
| PATCH | /api/puntos-acceso/{id}/habilitacion | habilitado |
| POST | /api/movimientos | codigoCredencial, puntoAccesoId, tipo |
| GET | /api/movimientos | empleadoId, desde, hasta, puntoAccesoId, resultado, pagina, tamano |

Creaciones de empleado, credencial y punto: HTTP 201. Lecturas, cambios y decisiones de acceso: 200. Un acceso **rechazado devuelve 200** porque la solicitud se procesó y quedó registrada; su resultado JSON es `Rechazado`. Errores de entrada: 400, entidad no encontrada: 404, duplicados: 409. Fallos internos: 500 sin exponer detalles. SQL no disponible en health: 503. Errores con formato Problem Details.

Historial: extremos de fechas inclusivos; filtros combinables; tamaño 1..100 y página >=1. Orden estable por fecha descendente y luego Id. Incluye total de registros y páginas; una página sin coincidencias retorna items vacío. Para abarcar un día use límites con hora explícita.

## Reglas e interpretación del enunciado

- Identificación única y no editable tras crear empleado; normalizada con Trim/uppercase. Desactivar no elimina empleados ni historial. El enunciado no pide reactivación.
- Código único, normalizado; no se reutiliza aunque la credencial esté revocada. Una revocación es definitiva; una credencial bloqueada no autoriza accesos. Se pueden asignar varias credenciales a un empleado activo.
- Se rechaza cualquier movimiento si el empleado está inactivo, la credencial no es vigente o el punto no está habilitado/existe. Se aplica a entradas y salidas por consistencia académica; no es un sistema de evacuación física.
- Se auditan también credenciales desconocidas y puntos inexistentes. EmpleadoId/CredencialId pueden ser null. PuntoAccesoId guarda el identificador solicitado sin FK para permitir estos intentos.
- Solicitudes malformadas (código vacío, GUID inválido, enum inválido) son errores 400, no intentos de acceso evaluables.
- El PDF menciona áreas restringidas pero no define permisos por empleado/área; esta versión valida habilitación del punto, sin inventar una matriz de autorizaciones.
- No se exige impedir entradas consecutivas ni horarios laborales. No se agregan esas reglas.
- RF-05 no aparece en el PDF: su numeración salta de RF-04 a RF-06.

## Arquitectura y trazabilidad

```text
API -> Application -> Domain
API -> Infrastructure -> Application / Domain
```

Domain no referencia EF ni ASP.NET. Application declara los puertos IEscritura/ILectura y los contratos CQRS. Infrastructure implementa puertos con DbContext. API registra implementaciones en DI y expone REST. Se usa un solo SQL Server, lo cual es compatible con CQRS (no exige dos bases ni un broker).

| Requisito | Código |
|---|---|
| RF-01 empleados | Domain/Modelo.cs: Empleado; Application/Comandos.cs y Consultas.cs |
| RF-02 credenciales | Domain/Modelo.cs: Credencial y CodigoCredencial |
| RF-03 decisiones y auditoría | MovimientoAcceso.Evaluar; Comandos.Handle(RegistrarMovimiento) |
| RF-04 historial paginado | Lectura.Filtrar / Historial; ConsultarHistorial |
| RF-06 puntos | PuntoAcceso; RegistrarPunto; ListarPuntos |
| SQL Server / EF Core | Infrastructure/AccesoDbContext.cs y Repositorios.cs |
| REST documentado | API/Program.cs, SGCAE.http y este documento |

## Sustentación

Consulte `GUIA-SUSTENTACION.md`. No afirme que las pruebas unitarias verifican una instancia SQL real: la comprobación integrada se hace con DEMOSTRACION.ps1 y /health/database.

## Git y entrega en GitHub

El entregable es el enlace al repositorio **público**. Si aún no está publicado, en una PowerShell desde esta carpeta:

```powershell
git status
gh auth login
gh repo create SGCAE-Seguimiento2 --public --source . --remote origin --push
gh repo view --web
```

`gh auth login` debe hacerlo usted de forma interactiva. Si ya existe origin o ese nombre de repositorio, no fuerce ni sobrescriba: revise `git remote -v` y use el repositorio correcto. Solo se publica esta carpeta, no otros proyectos ni documentos personales. Use únicamente datos ficticios para evidencias; nunca suba secretos ni registros de empleados reales.

## Límites de seguridad

Esta entrega es una API académica local sin autenticación/autorización, sin lector físico, sin control de identidad biométrico y sin políticas de permisos por área. El código de credencial se usa como identificador académico, no como contraseña segura. Los registros se conservan en SQL; no se permite borrarlos desde la API. No exponga el backend a Internet ni lo use en producción. Antes de un despliegue real se requiere Identity/JWT y roles, HTTPS validado, protección contra abuso, gestión segura de credenciales, políticas de retención y auditoría protegida.
