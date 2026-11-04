using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using PrimeVideoApp.Models;

namespace PrimeVideoApp.Data;

public partial class PrimeVideoDbContext : DbContext
{
    public PrimeVideoDbContext(DbContextOptions<PrimeVideoDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Content> Contents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Content>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("content_pkey");

            entity.ToTable("content");

            entity.HasIndex(e => e.SourceId, "content_source_id_key").IsUnique();

            entity.HasIndex(e => e.Title, "idx_content_title");

            entity.HasIndex(e => e.ContentType, "idx_content_type");

            entity.HasIndex(e => e.ReleaseYear, "idx_content_year");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.ContentType)
                .HasMaxLength(20)
                .HasColumnName("content_type");
            entity.Property(e => e.DateAdded).HasColumnName("date_added");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DurationText)
                .HasMaxLength(50)
                .HasColumnName("duration_text");
            entity.Property(e => e.DurationUnit)
                .HasMaxLength(20)
                .HasColumnName("duration_unit");
            entity.Property(e => e.DurationValue).HasColumnName("duration_value");
            entity.Property(e => e.Rating)
                .HasMaxLength(30)
                .HasColumnName("rating");
            entity.Property(e => e.ReleaseYear).HasColumnName("release_year");
            entity.Property(e => e.SourceId)
                .HasMaxLength(20)
                .HasColumnName("source_id");
            entity.Property(e => e.Title)
                .HasMaxLength(500)
                .HasColumnName("title");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
