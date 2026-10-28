using Microsoft.EntityFrameworkCore;
using SGCAE.Domain;
namespace SGCAE.Infrastructure;
public sealed class AccesoDbContext(DbContextOptions<AccesoDbContext> opciones) : DbContext(opciones)
{
    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<Credencial> Credenciales => Set<Credencial>();
    public DbSet<PuntoAcceso> Puntos => Set<PuntoAcceso>();
    public DbSet<MovimientoAcceso> Movimientos => Set<MovimientoAcceso>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        var e = b.Entity<Empleado>(); e.ToTable("Empleados"); e.HasKey(x => x.Id);
        e.Property(x => x.Identificacion).HasConversion(v => v.Valor, v => new Identificacion(v)).HasMaxLength(40).IsRequired();
        e.HasIndex(x => x.Identificacion).IsUnique();
        e.Property(x => x.Correo).HasConversion(v => v.Valor, v => new Correo(v)).HasMaxLength(254).IsRequired();
        e.Property(x => x.Nombres).HasMaxLength(100).IsRequired(); e.Property(x => x.Apellidos).HasMaxLength(100).IsRequired();
        e.Property(x => x.Cargo).HasMaxLength(100).IsRequired(); e.Property(x => x.Area).HasMaxLength(100).IsRequired();
        var c = b.Entity<Credencial>(); c.ToTable("Credenciales"); c.HasKey(x => x.Id);
        c.Property(x => x.Codigo).HasConversion(v => v.Valor, v => new CodigoCredencial(v)).HasMaxLength(80).IsRequired();
        c.HasIndex(x => x.Codigo).IsUnique(); c.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
        c.HasOne<Empleado>().WithMany().HasForeignKey(x => x.EmpleadoId).OnDelete(DeleteBehavior.Restrict);
        var p = b.Entity<PuntoAcceso>(); p.ToTable("PuntosAcceso"); p.HasKey(x => x.Id);
        p.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        var m = b.Entity<MovimientoAcceso>(); m.ToTable("MovimientosAcceso"); m.HasKey(x => x.Id);
        m.Property(x => x.CodigoPresentado).HasMaxLength(80).IsRequired(); m.Property(x => x.Motivo).HasMaxLength(200).IsRequired();
        m.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20); m.Property(x => x.Resultado).HasConversion<string>().HasMaxLength(20);
        m.HasIndex(x => new { x.FechaUtc, x.Id }); m.HasIndex(x => new { x.EmpleadoId, x.FechaUtc });
        m.HasIndex(x => new { x.PuntoAccesoId, x.FechaUtc }); m.HasIndex(x => new { x.Resultado, x.FechaUtc });
        m.HasOne<Empleado>().WithMany().HasForeignKey(x => x.EmpleadoId).OnDelete(DeleteBehavior.Restrict);
        m.HasOne<Credencial>().WithMany().HasForeignKey(x => x.CredencialId).OnDelete(DeleteBehavior.Restrict);
    }
}
