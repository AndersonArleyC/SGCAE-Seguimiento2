using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SGCAE.Application;
using SGCAE.Domain;
namespace SGCAE.Infrastructure;
public sealed class Escritura(AccesoDbContext db) : IEscritura
{
    public Task<Empleado?> Empleado(Guid id, CancellationToken ct) => db.Empleados.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> ExisteIdentificacion(Identificacion id, CancellationToken ct) => db.Empleados.AnyAsync(x => x.Identificacion == id, ct);
    public Task<bool> ExisteCodigo(CodigoCredencial codigo, CancellationToken ct) => db.Credenciales.AnyAsync(x => x.Codigo == codigo, ct);
    public Task<Credencial?> Credencial(Guid id, CancellationToken ct) => db.Credenciales.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Credencial?> Credencial(CodigoCredencial codigo, CancellationToken ct) => db.Credenciales.SingleOrDefaultAsync(x => x.Codigo == codigo, ct);
    public Task<PuntoAcceso?> Punto(Guid id, CancellationToken ct) => db.Puntos.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void Agregar(Empleado e) => db.Empleados.Add(e);
    public void Agregar(Credencial c) => db.Credenciales.Add(c);
    public void Agregar(PuntoAcceso p) => db.Puntos.Add(p);
    public void Agregar(MovimientoAcceso m) => db.Movimientos.Add(m);
    public async Task Guardar(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new ConflictoException("Identificación o código de credencial duplicado."); }
    }
    public async Task<T> EnTransaccion<T>(Func<Task<T>> accion, CancellationToken ct)
    {
        // Evita autorizar mientras otra transacción desactiva al empleado o bloquea su credencial.
        await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try { var resultado = await accion(); await transaccion.CommitAsync(ct); return resultado; }
        catch (SqlException ex) when (ex.Number == 1205)
        { throw new ConflictoException("Conflicto concurrente. Reintente la operación."); }
    }
}
public sealed class Lectura(AccesoDbContext db) : ILectura
{
    public async Task<EmpleadoDto?> Empleado(Guid id, CancellationToken ct) => (await db.Empleados.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct))?.Dto();
    public async Task<Pagina<EmpleadoDto>> Empleados(ListarEmpleados f, CancellationToken ct)
    {
        var q = db.Empleados.AsNoTracking().AsQueryable(); if (f.Activo.HasValue) q = q.Where(x => x.Activo == f.Activo);
        int total = await q.CountAsync(ct);
        var datos = await q.OrderBy(x => x.Apellidos).ThenBy(x => x.Id).Skip((f.Pagina - 1) * f.Tamano).Take(f.Tamano).ToListAsync(ct);
        return new(datos.Select(x => x.Dto()).ToArray(), f.Pagina, f.Tamano, total);
    }
    public async Task<CredencialDto?> Credencial(Guid id, CancellationToken ct) => (await db.Credenciales.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct))?.Dto();
    public async Task<IReadOnlyList<CredencialDto>> Credenciales(Guid empleadoId, CancellationToken ct) =>
        (await db.Credenciales.AsNoTracking().Where(x => x.EmpleadoId == empleadoId).OrderBy(x => x.Id).ToListAsync(ct)).Select(x => x.Dto()).ToArray();
    public async Task<IReadOnlyList<PuntoDto>> Puntos(CancellationToken ct) =>
        (await db.Puntos.AsNoTracking().OrderBy(x => x.Nombre).ThenBy(x => x.Id).ToListAsync(ct)).Select(x => x.Dto()).ToArray();
    public IQueryable<MovimientoAcceso> Filtrar(ConsultarHistorial f)
    {
        var q = db.Movimientos.AsNoTracking().AsQueryable();
        if (f.EmpleadoId.HasValue) q = q.Where(x => x.EmpleadoId == f.EmpleadoId);
        if (f.PuntoAccesoId.HasValue) q = q.Where(x => x.PuntoAccesoId == f.PuntoAccesoId);
        if (f.Resultado.HasValue) q = q.Where(x => x.Resultado == f.Resultado);
        if (f.Desde.HasValue) q = q.Where(x => x.FechaUtc >= f.Desde.Value);
        if (f.Hasta.HasValue) q = q.Where(x => x.FechaUtc <= f.Hasta.Value);
        return q;
    }
    public async Task<Pagina<MovimientoDto>> Historial(ConsultarHistorial f, CancellationToken ct)
    {
        var q = Filtrar(f); int total = await q.CountAsync(ct);
        var datos = await q.OrderByDescending(x => x.FechaUtc).ThenByDescending(x => x.Id)
            .Skip((f.Pagina - 1) * f.Tamano).Take(f.Tamano).ToListAsync(ct);
        return new(datos.Select(x => x.Dto()).ToArray(), f.Pagina, f.Tamano, total);
    }
}
