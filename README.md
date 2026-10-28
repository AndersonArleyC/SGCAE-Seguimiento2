# SGCAE

API REST para gestionar empleados, credenciales, puntos de acceso y movimientos de entrada y salida.

## Tecnologías

.NET 10, ASP.NET Core Web API, Entity Framework Core y SQL Server. Solución organizada en Domain, Application, Infrastructure, API y Tests, con Clean Architecture, DDD y CQRS.

## Ejecución

Requisitos: SDK .NET 10 y SQL Server. Desde la raíz del repositorio:

```powershell
dotnet build
dotnet test
$env:ConnectionStrings__Accesos = "Server=localhost;Database=SGCAE;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
```

Adapte el servidor a su instancia. `TrustServerCertificate=True` es solo para desarrollo local; en producción se debe validar el certificado.

Preparar una base nueva, solo la primera vez:

```powershell
dotnet run --project src/SGCAE.API -- --initialize-database
```

Se utiliza EnsureCreated: no actualiza esquemas existentes ni debe mezclarse con migraciones.

Iniciar:

```powershell
dotnet run --project src/SGCAE.API -- --urls http://localhost:5300
```

## API

- `/api/empleados`: registro, consulta, actualización y desactivación.
- `/api/credenciales`: asignación, consulta, bloqueo y revocación.
- `/api/puntos-acceso`: registro, consulta y habilitación.
- `/api/movimientos`: registro de entradas y salidas e historial con filtros y paginación.
- `/health` y `/health/database`: estado de la aplicación y de la conexión.

Las rutas, métodos y ejemplos están en `SGCAE.http`. Los enums se envían como texto; las fechas, en ISO 8601 con zona horaria. El historial admite filtros por empleado, fechas, punto y resultado, con `pagina` y `tamano` (1 a 100).

Los intentos autorizados y rechazados quedan registrados. Un rechazo procesado devuelve HTTP 200 con resultado `Rechazado`; solicitudes inválidas devuelven 400, entidades inexistentes 404 y duplicados 409.

## Alcance

Versión académica local sin autenticación ni autorización por roles. No debe exponerse en Internet ni utilizarse para controlar accesos físicos reales. Utilice datos ficticios.
