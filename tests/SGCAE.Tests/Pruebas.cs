using Microsoft.EntityFrameworkCore;
using SGCAE.Domain;
using SGCAE.Application;
using SGCAE.Infrastructure;
namespace SGCAE.Tests;
public sealed class PruebasDominio
{
    internal static Empleado Nuevo() => new(new Identificacion("123"), "Ana", "Prueba", new Correo("ana@example.com"), "Auxiliar", "Logística");
    [Fact] public void NormalizaObjetosValor()
    {
        Assert.Equal(new Identificacion("ABC"), new Identificacion(" abc "));
        Assert.Equal("ana@example.com", new Correo(" ANA@EXAMPLE.COM ").Valor);
        Assert.Equal(new CodigoCredencial("abc"), new CodigoCredencial(" ABC "));
    }
    [Theory] [InlineData("")] [InlineData("no-es-correo")]
    public void RechazaCorreoInvalido(string correo) => Assert.Throws<ReglaNegocioException>(() => new Correo(correo));
    [Fact] public void NoAsignaAEmpleadoInactivo()
    { var e = Nuevo(); e.Desactivar(); Assert.Throws<ReglaNegocioException>(() => new Credencial(e, new("A1"))); }
    [Fact] public void RevocacionEsDefinitiva()
    { var c = new Credencial(Nuevo(), new("A1")); c.Revocar(); Assert.Throws<ReglaNegocioException>(c.Bloquear); Assert.Equal(EstadoCredencial.Revocada, c.Estado); }
    [Theory] [InlineData(TipoMovimiento.Entrada)] [InlineData(TipoMovimiento.Salida)]
    public void AutorizaAmbosMovimientos(TipoMovimiento tipo)
    {
        var e = Nuevo(); var c = new Credencial(e, new("A1")); var p = new PuntoAcceso("Recepción", true);
        var m = MovimientoAcceso.Evaluar(c.Codigo, c, e, p, p.Id, tipo, DateTimeOffset.UtcNow);
        Assert.Equal(ResultadoAcceso.Autorizado, m.Resultado); Assert.Equal(e.Id, m.EmpleadoId);
    }
    [Theory] [InlineData("inactivo")] [InlineData("bloqueada")] [InlineData("revocada")]
    [InlineData("punto")] [InlineData("deshabilitado")] [InlineData("desconocida")]
    public void RechazaCondicionesInvalidas(string condicion)
    {
        var e = Nuevo(); Credencial? c = new(e, new("A1")); PuntoAcceso? p = new("Recepción", true); var id = p.Id;
        if (condicion == "inactivo") e.Desactivar();
        if (condicion == "bloqueada") c.Bloquear(); if (condicion == "revocada") c.Revocar();
        if (condicion == "punto") p = null; if (condicion == "deshabilitado") p!.CambiarHabilitacion(false);
        if (condicion == "desconocida") { c = null; }
        var m = MovimientoAcceso.Evaluar(new("A1"), c, c is null ? null : e, p, id, TipoMovimiento.Entrada, DateTimeOffset.UtcNow);
        Assert.Equal(ResultadoAcceso.Rechazado, m.Resultado); Assert.NotEmpty(m.Motivo);
        Assert.Equal(id, m.PuntoAccesoId);
    }
    [Theory] [InlineData(0,20)] [InlineData(1,101)] [InlineData(int.MaxValue,100)]
    public void PaginacionInvalida(int pagina, int tamano) => Assert.Throws<ReglaNegocioException>(() => Paginacion.Validar(pagina,tamano));
    [Fact] public void ModeloSqlTieneIndicesUnicosYForaneas()
    {
        using var db = SqlContext(); var sql = db.Database.GenerateCreateScript();
        Assert.Contains("CREATE UNIQUE INDEX [IX_Empleados_Identificacion]", sql);
        Assert.Contains("CREATE UNIQUE INDEX [IX_Credenciales_Codigo]", sql);
        Assert.Contains("FOREIGN KEY ([EmpleadoId])", sql);
        Assert.Contains("datetimeoffset", sql);
    }
    [Fact] public void FiltrosSeTraducenASqlAntesDePaginar()
    {
        using var db = SqlContext();
        var q = new Lectura(db).Filtrar(new ConsultarHistorial(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow, Guid.NewGuid(), ResultadoAcceso.Rechazado, 2, 10));
        var sql = q.OrderByDescending(x=>x.FechaUtc).ThenByDescending(x=>x.Id).Skip(10).Take(10).ToQueryString();
        Assert.Contains("WHERE", sql); Assert.Contains("OFFSET", sql); Assert.Contains("FETCH NEXT", sql);
        Assert.Contains("[Resultado]", sql); Assert.Contains("[EmpleadoId]", sql); Assert.Contains("[PuntoAccesoId]", sql);
        Assert.Contains("[FechaUtc] >=", sql); Assert.Contains("[FechaUtc] <=", sql);
        var codigoSql = db.Credenciales.Where(x => x.Codigo == new CodigoCredencial("A1")).ToQueryString();
        Assert.Contains("WHERE", codigoSql);
    }
    private static AccesoDbContext SqlContext() => new(new DbContextOptionsBuilder<AccesoDbContext>()
        .UseSqlServer("Server=localhost;Database=NoSeConecta;Integrated Security=True;Encrypt=True").Options);
}

public sealed class PruebasAplicacion
{
    [Fact] public async Task PersisteIntentoConCredencialDesconocida()
    {
        var fake = new Memoria(); var h = new Comandos(fake, TimeProvider.System);
        var r = await h.Handle(new RegistrarMovimiento("DESCONOCIDA", fake.PuntoExistente.Id, TipoMovimiento.Entrada), default);
        Assert.Equal(ResultadoAcceso.Rechazado, r.Resultado); Assert.Single(fake.Movimientos); Assert.Equal(1, fake.Guardados);
    }
    [Fact] public async Task PersisteIntentoAutorizadoYRechazadoTrasDesactivar()
    {
        var fake = new Memoria(); var h = new Comandos(fake, TimeProvider.System);
        var e = await h.Handle(new RegistrarEmpleado("123", "Ana", "Prueba", "ana@example.com", "Auxiliar", "Logística"), default);
        await h.Handle(new AsignarCredencial(e.Id,"A1"),default);
        Assert.Equal(ResultadoAcceso.Autorizado, (await h.Handle(new RegistrarMovimiento("A1",fake.PuntoExistente.Id,TipoMovimiento.Entrada),default)).Resultado);
        await h.Handle(new DesactivarEmpleado(e.Id),default);
        Assert.Equal(ResultadoAcceso.Rechazado, (await h.Handle(new RegistrarMovimiento("A1",fake.PuntoExistente.Id,TipoMovimiento.Entrada),default)).Resultado);
        Assert.Equal(2,fake.Movimientos.Count);
    }
    [Fact] public async Task IdentificacionDuplicadaEsConflicto()
    {
        var h = new Comandos(new Memoria(),TimeProvider.System); var c = new RegistrarEmpleado("123","Ana","Prueba","ana@example.com","Auxiliar","Logística");
        await h.Handle(c,default); await Assert.ThrowsAsync<ConflictoException>(() => h.Handle(c,default));
    }
    [Fact] public async Task CodigoDuplicadoEsConflicto()
    {
        var h = new Comandos(new Memoria(),TimeProvider.System);
        var e = await h.Handle(new RegistrarEmpleado("123","Ana","Prueba","ana@example.com","Auxiliar","Logística"),default);
        await h.Handle(new AsignarCredencial(e.Id,"abc"),default);
        await Assert.ThrowsAsync<ConflictoException>(()=>h.Handle(new AsignarCredencial(e.Id," ABC "),default));
    }
    private sealed class Memoria : IEscritura
    {
        public readonly List<Empleado> Empleados = []; public readonly List<Credencial> Credenciales = [];
        public readonly List<MovimientoAcceso> Movimientos = []; public int Guardados;
        public PuntoAcceso PuntoExistente = new("Recepción",true);
        public Task<Empleado?> Empleado(Guid id,CancellationToken ct) => Task.FromResult(Empleados.SingleOrDefault(x=>x.Id==id));
        public Task<bool> ExisteIdentificacion(Identificacion id,CancellationToken ct) => Task.FromResult(Empleados.Any(x=>x.Identificacion==id));
        public Task<bool> ExisteCodigo(CodigoCredencial c,CancellationToken ct) => Task.FromResult(Credenciales.Any(x=>x.Codigo==c));
        public Task<Credencial?> Credencial(Guid id,CancellationToken ct) => Task.FromResult(Credenciales.SingleOrDefault(x=>x.Id==id));
        public Task<Credencial?> Credencial(CodigoCredencial c,CancellationToken ct) => Task.FromResult(Credenciales.SingleOrDefault(x=>x.Codigo==c));
        public Task<PuntoAcceso?> Punto(Guid id,CancellationToken ct) => Task.FromResult(id==PuntoExistente.Id ? PuntoExistente : null);
        public void Agregar(Empleado e)=>Empleados.Add(e); public void Agregar(Credencial c)=>Credenciales.Add(c);
        public void Agregar(PuntoAcceso p)=>PuntoExistente=p; public void Agregar(MovimientoAcceso m)=>Movimientos.Add(m);
        public Task Guardar(CancellationToken ct) {Guardados++;return Task.CompletedTask;}
        public Task<T> EnTransaccion<T>(Func<Task<T>> f,CancellationToken ct)=>f();
    }
}
