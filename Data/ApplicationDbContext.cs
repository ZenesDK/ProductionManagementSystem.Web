#nullable enable
using Microsoft.EntityFrameworkCore;
using ProductionManagementSystem.Web.Models;

namespace ProductionManagementSystem.Web.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }
    public DbSet<ProductionLine> ProductionLines { get; set; }
    public DbSet<Material> Materials { get; set; }
    public DbSet<ProductMaterial> ProductMaterials { get; set; }
    public DbSet<WorkOrder> WorkOrders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Product - ProductMaterial
        modelBuilder.Entity<Product>()
            .HasMany(p => p.MaterialsNeeded)
            .WithOne(pm => pm.Product)
            .HasForeignKey(pm => pm.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Material - ProductMaterial
        modelBuilder.Entity<Material>()
            .HasMany(m => m.ProductsUsedIn)
            .WithOne(pm => pm.Material)
            .HasForeignKey(pm => pm.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);

        // ProductionLine - WorkOrder
        modelBuilder.Entity<ProductionLine>()
            .HasMany(pl => pl.AssignedWorkOrders)
            .WithOne(wo => wo.ProductionLine)
            .HasForeignKey(wo => wo.ProductionLineId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProductionLine>()
            .HasOne(pl => pl.CurrentWorkOrder)
            .WithMany()
            .HasForeignKey(pl => pl.CurrentWorkOrderId)
            .OnDelete(DeleteBehavior.SetNull);
        
        modelBuilder.Entity<ProductionLine>()
            .HasIndex(p => p.Name)
            .IsUnique();

        // WorkOrder - Product
        modelBuilder.Entity<WorkOrder>()
            .HasOne(wo => wo.Product)
            .WithMany(p => p.WorkOrders)
            .HasForeignKey(wo => wo.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Уникальные индексы
        modelBuilder.Entity<Material>()
            .HasIndex(m => m.Name)
            .IsUnique();

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Name)
            .IsUnique();

        // Составной первичный ключ для ProductMaterial
        modelBuilder.Entity<ProductMaterial>()
            .HasKey(t => new { t.ProductId, t.MaterialId });
    }
}