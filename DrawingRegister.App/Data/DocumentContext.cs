using Microsoft.EntityFrameworkCore;
using DrawingRegister.App.Models;
using System.IO;
using System.Text.Json;

namespace DrawingRegister.App.Data
{
    public class DocumentContext : DbContext
    {
        public DbSet<DocumentMetadata> Documents { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var dbPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "DrawingManager.db");

            optionsBuilder
                .UseSqlite($"Data Source={dbPath}")
                .EnableSensitiveDataLogging() // For better error messages
                .EnableDetailedErrors();      // For better error messages
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DocumentMetadata>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.DocumentNumber);
                entity.Property(e => e.DocumentNumber).IsRequired();
                entity.Property(e => e.FilePath).IsRequired();
                entity.Property(e => e.Package).IsRequired();
                
                // Project Info
                entity.Property(e => e.ProjectNumber).IsRequired();
                entity.Property(e => e.ProjectName).IsRequired();
                entity.Property(e => e.Discipline).IsRequired();
                
                // Make other properties optional
                entity.Property(e => e.Description).IsRequired(false);
                entity.Property(e => e.DocumentType).IsRequired(false);
                entity.Property(e => e.Size).IsRequired(false);
                entity.Property(e => e.ClientNumber).IsRequired(false);
                entity.Property(e => e.RegisterNumber).IsRequired(false);

                // Store RevisionHistory as JSON
                entity.Property(e => e.RevisionHistory)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<Dictionary<DateTime, RevisionInfo>>(v, (JsonSerializerOptions)null) ?? new());

                // Store Stakeholders as JSON
                entity.Property(e => e.Stakeholders)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions)null) ?? new());
            });
        }
    }
} 