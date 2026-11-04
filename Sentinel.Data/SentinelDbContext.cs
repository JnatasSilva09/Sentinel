using Microsoft.EntityFrameworkCore;
using Sentinel.Core.Entities;

namespace Sentinel.Data;

public class SentinelDbContext : DbContext
{
    public SentinelDbContext(DbContextOptions<SentinelDbContext> options) : base(options) { }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<SecurityEvent> Events => Set<SecurityEvent>();
    public DbSet<Alerta> Alerts => Set<Alerta>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentEvent> IncidentEvents => Set<IncidentEvent>();
    public DbSet<IncidentNote> IncidentNotes => Set<IncidentNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>(e =>
        {
            e.Property(d => d.Nome).IsRequired().HasMaxLength(100);
            e.Property(d => d.EnderecoIp).IsRequired().HasMaxLength(15);
            e.HasIndex(d => d.EnderecoIp).IsUnique();
            e.Property(d => d.SistemaOperacional).HasConversion<string>().HasMaxLength(20);
            e.Property(d => d.Tipo).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<SecurityEvent>(e =>
        {
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Gravidade).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Descricao).HasMaxLength(500);
            e.HasIndex(x => x.DataHora);
            e.HasOne(x => x.Device)
                .WithMany(d => d.Eventos)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Alerta>(e =>
        {
            e.Property(a => a.Regra).IsRequired().HasMaxLength(100);
            e.Property(a => a.Descricao).HasMaxLength(500);
            e.Property(a => a.Gravidade).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(a => a.DataHora);
            e.HasOne(a => a.Device)
                .WithMany(d => d.Alertas)
                .HasForeignKey(a => a.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Incident>(e =>
        {
            e.Property(i => i.Titulo).IsRequired().HasMaxLength(150);
            e.Property(i => i.Descricao).HasMaxLength(2000);
            e.Property(i => i.Risco).HasConversion<string>().HasMaxLength(20);
            e.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(i => i.Status);
            e.HasIndex(i => i.DataAbertura);

            // Um alerta vira no máximo um incidente. Apagar o alerta não apaga o incidente (histórico).
            e.HasOne(i => i.AlertaOrigem)
                .WithMany()
                .HasForeignKey(i => i.AlertaOrigemId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(i => i.AlertaOrigemId).IsUnique();
        });

        modelBuilder.Entity<IncidentEvent>(e =>
        {
            e.HasKey(x => new { x.IncidentId, x.SecurityEventId });
            e.HasOne(x => x.Incident)
                .WithMany(i => i.Eventos)
                .HasForeignKey(x => x.IncidentId);
            e.HasOne(x => x.SecurityEvent)
                .WithMany()
                .HasForeignKey(x => x.SecurityEventId);
        });

        modelBuilder.Entity<IncidentNote>(e =>
        {
            e.Property(n => n.Texto).IsRequired().HasMaxLength(1000);
            e.HasOne(n => n.Incident)
                .WithMany(i => i.Notas)
                .HasForeignKey(n => n.IncidentId);
        });
    }
}
