# Verificación realizada - 28 de octubre de 2026

- SDK: .NET 10.0.401.
- Compilación: 5 proyectos, 0 errores y 0 advertencias.
- Pruebas automatizadas: 22 aprobadas, 0 fallidas, 0 omitidas.
- SQL Server local: base nueva SGCAE creada con EF Core EnsureCreated. Tablas, índices únicos y relaciones generados correctamente.
- API local: http://localhost:5300. `/health/database`: healthy. Página de documentación: HTTP 200.
- DEMOSTRACION.ps1: 10 comprobaciones integradas aprobadas contra SQL Server (entrada, salida, actualización, bloqueo, revocación, punto deshabilitado, empleado inactivo, código desconocido, filtros/paginación y segunda página).
- Se conservaron datos ficticios DEMO: un empleado, dos credenciales, un punto y siete movimientos (dos autorizados y cinco rechazados). No se eliminaron datos existentes.
- Comprobaciones adicionales: página 0 -> HTTP 400; tamaño 101 -> HTTP 400; fechas invertidas -> HTTP 400; empleado inexistente -> HTTP 404.
- Fecha de movimiento comprobada en JSON con offset +00:00 (UTC).

Los resultados documentan esta ejecución, no sustituyen volver a probar en otro equipo. Las pruebas de modelo generan SQL sin conectarse; el script de demostración sí realiza solicitudes reales contra la API y SQL Server. No se realizaron pruebas de carga, seguridad ni validación de un sistema de control físico real.
