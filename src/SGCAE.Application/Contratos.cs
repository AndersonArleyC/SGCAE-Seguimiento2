using SGCAE.Domain;
namespace SGCAE.Application;

public interface ICommand<T>;
public interface IQuery<T>;
public interface ICommandHandler<in T, R> where T : ICommand<R> { Task<R> Handle(T comando, CancellationToken ct); }
public interface IQueryHandler<in T, R> where T : IQuery<R> { Task<R> Handle(T consulta, CancellationToken ct); }
public sealed class NoEncontradoException(string mensaje) : Exception(mensaje);
public sealed class ConflictoException(string mensaje) : Exception(mensaje);

public sealed record EmpleadoDto(Guid Id, string Identificacion, string Nombres, string Apellidos, string Correo, string Cargo, string Area, bool Activo);
public sealed record CredencialDto(Guid Id, Guid EmpleadoId, string Codigo, EstadoCredencial Estado);
public sealed record PuntoDto(Guid Id, string Nombre, bool Habilitado);
public sealed record MovimientoDto(Guid Id, Guid? EmpleadoId, Guid? CredencialId, Guid PuntoAccesoId, TipoMovimiento Tipo,
    DateTimeOffset FechaUtc, ResultadoAcceso Resultado, string Motivo);
public sealed record Pagina<T>(IReadOnlyList<T> Items, int PaginaActual, int Tamano, int Total)
{
    public int TotalPaginas => (int)Math.Ceiling((double)Total / Tamano);
}
public static class Mapeo
{
    public static EmpleadoDto Dto(this Empleado e) => new(e.Id, e.Identificacion.Valor, e.Nombres, e.Apellidos, e.Correo.Valor, e.Cargo, e.Area, e.Activo);
    public static CredencialDto Dto(this Credencial c) => new(c.Id, c.EmpleadoId, c.Codigo.Valor, c.Estado);
    public static PuntoDto Dto(this PuntoAcceso p) => new(p.Id, p.Nombre, p.Habilitado);
    public static MovimientoDto Dto(this MovimientoAcceso m) => new(m.Id, m.EmpleadoId, m.CredencialId, m.PuntoAccesoId, m.Tipo, m.FechaUtc, m.Resultado, m.Motivo);
}

// Puertos: Application no conoce DbContext ni SQL Server.
public interface IEscritura
{
    Task<Empleado?> Empleado(Guid id, CancellationToken ct);
    Task<bool> ExisteIdentificacion(Identificacion identificacion, CancellationToken ct);
    Task<bool> ExisteCodigo(CodigoCredencial codigo, CancellationToken ct);
    Task<Credencial?> Credencial(Guid id, CancellationToken ct);
    Task<Credencial?> Credencial(CodigoCredencial codigo, CancellationToken ct);
    Task<PuntoAcceso?> Punto(Guid id, CancellationToken ct);
    void Agregar(Empleado empleado);
    void Agregar(Credencial credencial);
    void Agregar(PuntoAcceso punto);
    void Agregar(MovimientoAcceso movimiento);
    Task Guardar(CancellationToken ct);
    Task<T> EnTransaccion<T>(Func<Task<T>> accion, CancellationToken ct);
}
public interface ILectura
{
    Task<EmpleadoDto?> Empleado(Guid id, CancellationToken ct);
    Task<Pagina<EmpleadoDto>> Empleados(ListarEmpleados filtro, CancellationToken ct);
    Task<CredencialDto?> Credencial(Guid id, CancellationToken ct);
    Task<IReadOnlyList<CredencialDto>> Credenciales(Guid empleadoId, CancellationToken ct);
    Task<IReadOnlyList<PuntoDto>> Puntos(CancellationToken ct);
    Task<Pagina<MovimientoDto>> Historial(ConsultarHistorial filtro, CancellationToken ct);
}
public static class Paginacion
{
    public static void Validar(int pagina, int tamano)
    {
        if (pagina < 1 || tamano is < 1 or > 100 || (long)(pagina - 1) * tamano > int.MaxValue)
            throw new ReglaNegocioException("Página debe ser mayor que cero y tamaño entre 1 y 100, sin desbordamiento.");
    }
}
