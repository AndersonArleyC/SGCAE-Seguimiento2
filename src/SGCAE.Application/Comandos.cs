using SGCAE.Domain;
namespace SGCAE.Application;

public sealed record RegistrarEmpleado(string Identificacion, string Nombres, string Apellidos, string Correo, string Cargo, string Area) : ICommand<EmpleadoDto>;
public sealed record ActualizarEmpleado(Guid Id, string Nombres, string Apellidos, string Correo, string Cargo, string Area) : ICommand<EmpleadoDto>;
public sealed record DesactivarEmpleado(Guid Id) : ICommand<EmpleadoDto>;
public sealed record AsignarCredencial(Guid EmpleadoId, string Codigo) : ICommand<CredencialDto>;
public sealed record BloquearCredencial(Guid Id) : ICommand<CredencialDto>;
public sealed record RevocarCredencial(Guid Id) : ICommand<CredencialDto>;
public sealed record RegistrarPunto(string Nombre, bool Habilitado = true) : ICommand<PuntoDto>;
public sealed record HabilitarPunto(Guid Id, bool Habilitado) : ICommand<PuntoDto>;
public sealed record RegistrarMovimiento(string CodigoCredencial, Guid PuntoAccesoId, TipoMovimiento Tipo) : ICommand<MovimientoDto>;

public sealed class Comandos(IEscritura db, TimeProvider reloj) :
    ICommandHandler<RegistrarEmpleado, EmpleadoDto>, ICommandHandler<ActualizarEmpleado, EmpleadoDto>,
    ICommandHandler<DesactivarEmpleado, EmpleadoDto>, ICommandHandler<AsignarCredencial, CredencialDto>,
    ICommandHandler<BloquearCredencial, CredencialDto>, ICommandHandler<RevocarCredencial, CredencialDto>,
    ICommandHandler<RegistrarPunto, PuntoDto>, ICommandHandler<HabilitarPunto, PuntoDto>,
    ICommandHandler<RegistrarMovimiento, MovimientoDto>
{
    public async Task<EmpleadoDto> Handle(RegistrarEmpleado c, CancellationToken ct)
    {
        var identificacion = new Identificacion(c.Identificacion);
        if (await db.ExisteIdentificacion(identificacion, ct)) throw new ConflictoException("La identificación ya está registrada.");
        var e = new Empleado(identificacion, c.Nombres, c.Apellidos, new Correo(c.Correo), c.Cargo, c.Area);
        db.Agregar(e); await db.Guardar(ct); return e.Dto();
    }
    public async Task<EmpleadoDto> Handle(ActualizarEmpleado c, CancellationToken ct)
    {
        var e = await db.Empleado(c.Id, ct) ?? throw new NoEncontradoException("Empleado no encontrado.");
        e.Actualizar(c.Nombres, c.Apellidos, new Correo(c.Correo), c.Cargo, c.Area);
        await db.Guardar(ct); return e.Dto();
    }
    public Task<EmpleadoDto> Handle(DesactivarEmpleado c, CancellationToken ct) => db.EnTransaccion(async () =>
    {
        var e = await db.Empleado(c.Id, ct) ?? throw new NoEncontradoException("Empleado no encontrado.");
        e.Desactivar(); await db.Guardar(ct); return e.Dto();
    }, ct);
    public Task<CredencialDto> Handle(AsignarCredencial c, CancellationToken ct) => db.EnTransaccion(async () =>
    {
        var e = await db.Empleado(c.EmpleadoId, ct) ?? throw new NoEncontradoException("Empleado no encontrado.");
        var codigo = new CodigoCredencial(c.Codigo);
        if (await db.ExisteCodigo(codigo, ct)) throw new ConflictoException("El código ya está asignado.");
        var credencial = new Credencial(e, codigo); db.Agregar(credencial);
        await db.Guardar(ct); return credencial.Dto();
    }, ct);
    public Task<CredencialDto> Handle(BloquearCredencial c, CancellationToken ct) => CambiarCredencial(c.Id, false, ct);
    public Task<CredencialDto> Handle(RevocarCredencial c, CancellationToken ct) => CambiarCredencial(c.Id, true, ct);
    private Task<CredencialDto> CambiarCredencial(Guid id, bool revocar, CancellationToken ct) => db.EnTransaccion(async () =>
    {
        var c = await db.Credencial(id, ct) ?? throw new NoEncontradoException("Credencial no encontrada.");
        if (revocar) c.Revocar(); else c.Bloquear();
        await db.Guardar(ct); return c.Dto();
    }, ct);
    public async Task<PuntoDto> Handle(RegistrarPunto c, CancellationToken ct)
    {
        var p = new PuntoAcceso(c.Nombre, c.Habilitado); db.Agregar(p); await db.Guardar(ct); return p.Dto();
    }
    public Task<PuntoDto> Handle(HabilitarPunto c, CancellationToken ct) => db.EnTransaccion(async () =>
    {
        var p = await db.Punto(c.Id, ct) ?? throw new NoEncontradoException("Punto no encontrado.");
        p.CambiarHabilitacion(c.Habilitado); await db.Guardar(ct); return p.Dto();
    }, ct);
    public Task<MovimientoDto> Handle(RegistrarMovimiento c, CancellationToken ct) => db.EnTransaccion(async () =>
    {
        var codigo = new CodigoCredencial(c.CodigoCredencial);
        var credencial = await db.Credencial(codigo, ct);
        var empleado = credencial is null ? null : await db.Empleado(credencial.EmpleadoId, ct);
        var punto = await db.Punto(c.PuntoAccesoId, ct);
        var m = MovimientoAcceso.Evaluar(codigo, credencial, empleado, punto, c.PuntoAccesoId, c.Tipo, reloj.GetUtcNow());
        db.Agregar(m); await db.Guardar(ct); return m.Dto();
    }, ct);
}
