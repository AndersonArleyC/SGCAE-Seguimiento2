# Guía de sustentación SGCAE

## Antes de comenzar

Siga los comandos de README.md: build, test, conexión SQL, inicialización solamente la primera vez y run en localhost:5300. Tenga Visual Studio 2026 con SGCAE.sln abierto, SQL Server Management Studio, dos PowerShell y el repositorio de GitHub. No inicie la presentación esperando a que se instale SQL Server o .NET.

## 1. Explicar el propósito

"Implementé un backend de control de accesos de empleados. Administra empleados, credenciales y puntos; decide si un movimiento se autoriza y conserva también los intentos rechazados. El historial se consulta con filtros y paginación."

## 2. Mostrar Clean Architecture

En el Explorador de soluciones de la derecha, expanda la flecha junto a cada proyecto. Un doble clic sobre el nombre del proyecto puede abrir el XML .csproj: eso no es el código de negocio. Expanda sus carpetas y haga doble clic sobre los archivos .cs.

- Abra `src/SGCAE.Domain/Modelo.cs`: reglas sin EF ni controladores.
- Abra `src/SGCAE.Application/Contratos.cs`: puertos ILectura e IEscritura.
- Abra `src/SGCAE.Infrastructure/Repositorios.cs`: implementaciones SQL de los puertos.
- Abra `src/SGCAE.API/Program.cs`: composición e inyección de dependencias.

Diga: "Las dependencias apuntan hacia el dominio; la infraestructura implementa las interfaces que define Application."

## 3. Mostrar DDD (no BDD)

En `Modelo.cs`, busque con Ctrl+F:

- `class Empleado`: entidad con Id; setters privados; Desactivar y Actualizar.
- `record Identificacion`, `record Correo`, `record CodigoCredencial`: objetos de valor con validación y comparación por valor.
- `class Credencial`: invariante de no asignar a empleado inactivo, estados, revocación definitiva.
- `MovimientoAcceso Evaluar`: decisión de negocio, fecha UTC y motivo de rechazo.
- `IAggregateRoot`: raíces de agregado; referencias por identificador entre agregados.

Diga: "La lógica no depende de que el controlador recuerde validar los estados; está encapsulada en el dominio. La unicidad global se complementa con índices de base de datos."

## 4. Mostrar CQRS

Abra `Application/Comandos.cs`: RegistrarEmpleado, AsignarCredencial y RegistrarMovimiento modifican estado. Abra `Application/Consultas.cs`: ObtenerEmpleado y ConsultarHistorial solo consultan. En Contratos.cs muestre ICommand/IQuery y sus handlers. Los handlers se invocan directamente desde endpoints mediante DI; no hace falta MediatR para aplicar CQRS.

## 5. Mostrar EF Core / SQL Server

Abra `Infrastructure/AccesoDbContext.cs`: DbSets, conversiones de objetos de valor, HasIndex(...).IsUnique(), relaciones y restricciones de borrado. Abra `Repositorios.cs`: AsNoTracking, Where, CountAsync, Skip, Take y SaveChangesAsync. Los filtros y paginación se traducen a SQL antes de materializar; no se cargan todos los movimientos para filtrar en memoria.

Muestre `Escritura.EnTransaccion`: nivel Serializable para coordinar evaluación y cambios de estado. Si existe un conflicto/deadlock se debe reintentar; no significa que el acceso esté autorizado.

## 6. Demostrar API con datos ficticios

Ejecute DEMOSTRACION.ps1 en la segunda terminal. Explique cada línea OK y muestre:

1. Registro y actualización de empleado.
2. Asignación de credencial y creación del punto.
3. Entrada y salida autorizadas.
4. Bloqueo/revocación y acceso rechazado.
5. Punto deshabilitado y acceso rechazado.
6. Empleado inactivo y acceso rechazado.
7. Código desconocido y auditoría.
8. Historial filtrado de dos elementos por página y segunda página.

Para ir paso a paso abra SGCAE.http con Visual Studio o Postman. Ejecute peticiones en orden y copie los Id devueltos en las variables de arriba. Las rutas GET pueden abrirse en el navegador, pero POST/PUT/PATCH necesitan un cliente HTTP.

## 7. Mostrar persistencia real en SSMS

Conéctese a la instancia usada en la cadena de conexión, abra Nueva consulta y ejecute:

```sql
USE SGCAE;
SELECT TOP (20) * FROM dbo.Empleados;
SELECT TOP (20) * FROM dbo.Credenciales;
SELECT TOP (20) * FROM dbo.PuntosAcceso;
SELECT TOP (30) * FROM dbo.MovimientosAcceso ORDER BY FechaUtc DESC, Id DESC;
```

Muestre la columna Resultado y los motivos, incluyendo desconocidos. Se usa desactivación lógica: no se borra al empleado para preservar trazabilidad.

## 8. Mostrar pruebas y GitHub

En PowerShell `dotnet test`; o menú Prueba > Explorador de pruebas > Ejecutar todas. Abra tests/SGCAE.Tests/Pruebas.cs. Distinga pruebas puras de dominio, casos de uso con doble en memoria y generación/traducción SQL sin conexión. DEMOSTRACION.ps1 verifica el flujo integrado real cuando SQL está disponible.

Finalmente muestre el repositorio público en GitHub, README y commits. **La entrega no está completa hasta compartir ese enlace.** No afirme que ya está publicado si aún falta autenticación.

## Preguntas frecuentes

- ¿Por qué no hay interfaz de empleados? El enunciado exige backend REST; la página inicial solo documenta y el cliente es HTTP.
- ¿Por qué hay un solo servidor/base? Clean Architecture y CQRS no exigen microservicios ni bases separadas.
- ¿Por qué un rechazo tiene HTTP 200? Se procesó y auditó correctamente; la decisión de negocio es Rechazado. Un cuerpo inválido sí devuelve 400.
- ¿Cómo se evitan duplicados? Validación previa y restricción única en SQL como garantía final.
- ¿Qué falta para producción? Autenticación, roles, HTTPS, protección de credenciales, auditoría protegida y permisos por área.
