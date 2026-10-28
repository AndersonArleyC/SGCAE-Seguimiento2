using SGCAE.Domain;
namespace SGCAE.Application;
public sealed record ObtenerEmpleado(Guid Id) : IQuery<EmpleadoDto>;
public sealed record ListarEmpleados(int Pagina = 1, int Tamano = 20, bool? Activo = null) : IQuery<Pagina<EmpleadoDto>>;
public sealed record ObtenerCredencial(Guid Id) : IQuery<CredencialDto>;
public sealed record CredencialesEmpleado(Guid EmpleadoId) : IQuery<IReadOnlyList<CredencialDto>>;
public sealed record ListarPuntos : IQuery<IReadOnlyList<PuntoDto>>;
public sealed record ConsultarHistorial(Guid? EmpleadoId = null, DateTimeOffset? Desde = null, DateTimeOffset? Hasta = null,
    Guid? PuntoAccesoId = null, ResultadoAcceso? Resultado = null, int Pagina = 1, int Tamano = 20) : IQuery<Pagina<MovimientoDto>>;
public sealed class Consultas(ILectura db) : IQueryHandler<ObtenerEmpleado, EmpleadoDto>,
    IQueryHandler<ListarEmpleados, Pagina<EmpleadoDto>>, IQueryHandler<ObtenerCredencial, CredencialDto>,
    IQueryHandler<CredencialesEmpleado, IReadOnlyList<CredencialDto>>, IQueryHandler<ListarPuntos, IReadOnlyList<PuntoDto>>,
    IQueryHandler<ConsultarHistorial, Pagina<MovimientoDto>>
{
    public async Task<EmpleadoDto> Handle(ObtenerEmpleado q, CancellationToken ct) => await db.Empleado(q.Id, ct) ?? throw new NoEncontradoException("Empleado no encontrado.");
    public Task<Pagina<EmpleadoDto>> Handle(ListarEmpleados q, CancellationToken ct)
    { Paginacion.Validar(q.Pagina, q.Tamano); return db.Empleados(q, ct); }
    public async Task<CredencialDto> Handle(ObtenerCredencial q, CancellationToken ct) => await db.Credencial(q.Id, ct) ?? throw new NoEncontradoException("Credencial no encontrada.");
    public async Task<IReadOnlyList<CredencialDto>> Handle(CredencialesEmpleado q, CancellationToken ct)
    {
        _ = await db.Empleado(q.EmpleadoId, ct) ?? throw new NoEncontradoException("Empleado no encontrado.");
        return await db.Credenciales(q.EmpleadoId, ct);
    }
    public Task<IReadOnlyList<PuntoDto>> Handle(ListarPuntos q, CancellationToken ct) => db.Puntos(ct);
    public Task<Pagina<MovimientoDto>> Handle(ConsultarHistorial q, CancellationToken ct)
    {
        Paginacion.Validar(q.Pagina, q.Tamano);
        if (q.Desde > q.Hasta) throw new ReglaNegocioException("Desde no puede ser posterior a Hasta.");
        if (q.Resultado.HasValue && !Enum.IsDefined(q.Resultado.Value)) throw new ReglaNegocioException("Resultado no válido.");
        return db.Historial(q, ct);
    }
}
