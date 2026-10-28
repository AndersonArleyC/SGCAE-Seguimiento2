namespace SGCAE.Domain;

public sealed class ReglaNegocioException(string mensaje) : Exception(mensaje);
public enum EstadoCredencial { Vigente, Bloqueada, Revocada }
public enum TipoMovimiento { Entrada, Salida }
public enum ResultadoAcceso { Autorizado, Rechazado }

public static class Texto
{
    public static string Requerido(string? valor, string campo, int max)
    {
        valor = valor?.Trim();
        if (string.IsNullOrWhiteSpace(valor) || valor.Length > max)
            throw new ReglaNegocioException($"{campo} es obligatorio y admite hasta {max} caracteres.");
        return valor;
    }
}

// Objetos de valor: no tienen identidad; encapsulan normalización y validación.
public sealed record Identificacion
{
    public string Valor { get; }
    public Identificacion(string valor) => Valor = Texto.Requerido(valor, "Identificación", 40).ToUpperInvariant();
}
public sealed record Correo
{
    public string Valor { get; }
    public Correo(string valor)
    {
        valor = Texto.Requerido(valor, "Correo", 254).ToLowerInvariant();
        if (!System.Net.Mail.MailAddress.TryCreate(valor, out var direccion) || direccion.Address != valor)
            throw new ReglaNegocioException("El correo no es válido.");
        Valor = valor;
    }
}
public sealed record CodigoCredencial
{
    public string Valor { get; }
    public CodigoCredencial(string valor) => Valor = Texto.Requerido(valor, "Código de credencial", 80).ToUpperInvariant();
}

public abstract class Entidad
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}
public interface IAggregateRoot;

public sealed class Empleado : Entidad, IAggregateRoot
{
    public Identificacion Identificacion { get; private set; } = null!;
    public string Nombres { get; private set; } = null!;
    public string Apellidos { get; private set; } = null!;
    public Correo Correo { get; private set; } = null!;
    public string Cargo { get; private set; } = null!;
    public string Area { get; private set; } = null!;
    public bool Activo { get; private set; } = true;
    private Empleado() { }
    public Empleado(Identificacion identificacion, string nombres, string apellidos, Correo correo, string cargo, string area)
    {
        Identificacion = identificacion;
        Actualizar(nombres, apellidos, correo, cargo, area);
    }
    public void Actualizar(string nombres, string apellidos, Correo correo, string cargo, string area)
    {
        Nombres = Texto.Requerido(nombres, "Nombres", 100);
        Apellidos = Texto.Requerido(apellidos, "Apellidos", 100);
        Correo = correo;
        Cargo = Texto.Requerido(cargo, "Cargo", 100);
        Area = Texto.Requerido(area, "Área", 100);
    }
    public void Desactivar() => Activo = false;
}
public sealed class Credencial : Entidad, IAggregateRoot
{
    public Guid EmpleadoId { get; private set; }
    public CodigoCredencial Codigo { get; private set; } = null!;
    public EstadoCredencial Estado { get; private set; } = EstadoCredencial.Vigente;
    private Credencial() { }
    public Credencial(Empleado empleado, CodigoCredencial codigo)
    {
        if (!empleado.Activo) throw new ReglaNegocioException("No se puede asignar una credencial a un empleado inactivo.");
        EmpleadoId = empleado.Id;
        Codigo = codigo;
    }
    public void Bloquear()
    {
        if (Estado == EstadoCredencial.Revocada) throw new ReglaNegocioException("Una credencial revocada no puede bloquearse.");
        Estado = EstadoCredencial.Bloqueada;
    }
    public void Revocar() => Estado = EstadoCredencial.Revocada;
}
public sealed class PuntoAcceso : Entidad, IAggregateRoot
{
    public string Nombre { get; private set; } = null!;
    public bool Habilitado { get; private set; }
    private PuntoAcceso() { }
    public PuntoAcceso(string nombre, bool habilitado)
    {
        Nombre = Texto.Requerido(nombre, "Nombre del punto", 120);
        Habilitado = habilitado;
    }
    public void CambiarHabilitacion(bool habilitado) => Habilitado = habilitado;
}

// Un movimiento es inmutable desde la API. Los intentos desconocidos conservan
// el punto solicitado aunque no exista (no se aplica FK a ese identificador).
public sealed class MovimientoAcceso : Entidad, IAggregateRoot
{
    public Guid? EmpleadoId { get; private set; }
    public Guid? CredencialId { get; private set; }
    public Guid PuntoAccesoId { get; private set; }
    public string CodigoPresentado { get; private set; } = null!;
    public TipoMovimiento Tipo { get; private set; }
    public DateTimeOffset FechaUtc { get; private set; }
    public ResultadoAcceso Resultado { get; private set; }
    public string Motivo { get; private set; } = null!;
    private MovimientoAcceso() { }
    public static MovimientoAcceso Evaluar(CodigoCredencial codigo, Credencial? credencial, Empleado? empleado,
        PuntoAcceso? punto, Guid puntoSolicitado, TipoMovimiento tipo, DateTimeOffset fecha)
    {
        if (!Enum.IsDefined(tipo)) throw new ReglaNegocioException("Tipo de movimiento no válido.");
        if (puntoSolicitado == Guid.Empty) throw new ReglaNegocioException("El identificador del punto es obligatorio.");
        string? rechazo = credencial is null ? "Credencial desconocida" :
            credencial.Codigo != codigo ? "La credencial no corresponde al código presentado" :
            empleado is null || empleado.Id != credencial.EmpleadoId ? "Empleado no encontrado" :
            !empleado.Activo ? "Empleado inactivo" :
            credencial.Estado != EstadoCredencial.Vigente ? $"Credencial {credencial.Estado.ToString().ToLowerInvariant()}" :
            punto is null || punto.Id != puntoSolicitado ? "Punto de acceso desconocido" :
            !punto.Habilitado ? "Punto de acceso deshabilitado" : null;
        return new MovimientoAcceso
        {
            EmpleadoId = empleado?.Id, CredencialId = credencial?.Id, PuntoAccesoId = puntoSolicitado,
            CodigoPresentado = codigo.Valor, Tipo = tipo, FechaUtc = fecha.ToUniversalTime(),
            Resultado = rechazo is null ? ResultadoAcceso.Autorizado : ResultadoAcceso.Rechazado,
            Motivo = rechazo ?? "Acceso autorizado"
        };
    }
}
