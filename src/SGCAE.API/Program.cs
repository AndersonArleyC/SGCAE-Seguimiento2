using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SGCAE.Domain;
using SGCAE.Application;
using SGCAE.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<AccesoDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Accesos")));
builder.Services.AddScoped<IEscritura, Escritura>(); builder.Services.AddScoped<ILectura, Lectura>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<Comandos>(); builder.Services.AddScoped<Consultas>();
var app = builder.Build();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (Exception ex) when (ex is ReglaNegocioException or NoEncontradoException or ConflictoException)
    {
        var status = ex is NoEncontradoException ? 404 : ex is ConflictoException ? 409 : 400;
        await Results.Problem(statusCode: status, title: "No se pudo completar la operación", detail: ex.Message).ExecuteAsync(context);
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error en la solicitud {TraceId}", context.TraceIdentifier);
        await Results.Problem(statusCode: 500, title: "Error interno. Verifique el servidor y la conexión SQL.",
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
    }
});
app.UseDefaultFiles(); app.UseStaticFiles();

// Preparación explícita para una base nueva; nunca elimina ni recrea bases existentes.
if (args.Contains("--initialize-database"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AccesoDbContext>().Database.EnsureCreatedAsync();
    Console.WriteLine("Base SGCAE preparada (sin datos personales de ejemplo).");
    return;
}

app.MapGet("/health", () => Results.Ok(new { servicio = "SGCAE", estado = "running" }));
app.MapGet("/health/database", async (AccesoDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { estado = "healthy" }) : Results.Problem(statusCode: 503, title: "SQL Server no disponible"));

var empleados = app.MapGroup("/api/empleados");
empleados.MapPost("/", async (RegistrarEmpleado c, Comandos h, CancellationToken ct) =>
{ var e = await h.Handle(c, ct); return Results.Created($"/api/empleados/{e.Id}", e); });
empleados.MapGet("/", (int? pagina, int? tamano, bool? activo, Consultas h, CancellationToken ct) => h.Handle(new ListarEmpleados(pagina ?? 1, tamano ?? 20, activo), ct));
empleados.MapGet("/{id:guid}", (Guid id, Consultas h, CancellationToken ct) => h.Handle(new ObtenerEmpleado(id), ct));
empleados.MapPut("/{id:guid}", (Guid id, DatosEmpleado c, Comandos h, CancellationToken ct) => h.Handle(new ActualizarEmpleado(id, c.Nombres, c.Apellidos, c.Correo, c.Cargo, c.Area), ct));
empleados.MapPatch("/{id:guid}/desactivar", (Guid id, Comandos h, CancellationToken ct) => h.Handle(new DesactivarEmpleado(id), ct));
empleados.MapGet("/{id:guid}/credenciales", (Guid id, Consultas h, CancellationToken ct) => h.Handle(new CredencialesEmpleado(id), ct));

var credenciales = app.MapGroup("/api/credenciales");
credenciales.MapPost("/", async (AsignarCredencial c, Comandos h, CancellationToken ct) =>
{ var r = await h.Handle(c, ct); return Results.Created($"/api/credenciales/{r.Id}", r); });
credenciales.MapGet("/{id:guid}", (Guid id, Consultas h, CancellationToken ct) => h.Handle(new ObtenerCredencial(id), ct));
credenciales.MapPatch("/{id:guid}/bloquear", (Guid id, Comandos h, CancellationToken ct) => h.Handle(new BloquearCredencial(id), ct));
credenciales.MapPatch("/{id:guid}/revocar", (Guid id, Comandos h, CancellationToken ct) => h.Handle(new RevocarCredencial(id), ct));

app.MapPost("/api/puntos-acceso", async (RegistrarPunto c, Comandos h, CancellationToken ct) =>
{ var r = await h.Handle(c, ct); return Results.Created("/api/puntos-acceso", r); });
app.MapGet("/api/puntos-acceso", (Consultas h, CancellationToken ct) => h.Handle(new ListarPuntos(), ct));
app.MapPatch("/api/puntos-acceso/{id:guid}/habilitacion", (Guid id, EstadoPunto c, Comandos h, CancellationToken ct) => h.Handle(new HabilitarPunto(id, c.Habilitado), ct));
app.MapPost("/api/movimientos", async (RegistrarMovimiento c, Comandos h, CancellationToken ct) => Results.Ok(await h.Handle(c, ct)));
app.MapGet("/api/movimientos", (Guid? empleadoId, DateTimeOffset? desde, DateTimeOffset? hasta, Guid? puntoAccesoId,
    ResultadoAcceso? resultado, int? pagina, int? tamano, Consultas h, CancellationToken ct) =>
    h.Handle(new ConsultarHistorial(empleadoId, desde, hasta, puntoAccesoId, resultado, pagina ?? 1, tamano ?? 20), ct));

app.Run();
public sealed record DatosEmpleado(string Nombres, string Apellidos, string Correo, string Cargo, string Area);
public sealed record EstadoPunto(bool Habilitado);
