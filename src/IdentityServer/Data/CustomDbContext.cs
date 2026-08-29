﻿using System.Xml.Linq;
using IdentityServer.Models;
using IdentityServer.Models.Custom;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Data
{
    public class CustomDbContext : DbContext
    {
        public CustomDbContext(DbContextOptions<CustomDbContext> opts) : base(opts)
        {
        }

        public DbSet<View> View { get; set; }
        public DbSet<ViewType> ViewType { get; set; }
        public DbSet<UserChangeLog> UserChangeLogs { get; set; }
        public DbSet<UserConsent> UserConsent { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // md5 sha256 sha512

            //modelBuilder.Entity<CustomUser>().HasData(
            //    new CustomUser() { Id = 1, Email = "fcakiroglu@outlook.com", Password = "password", City = "istanbul", UserName = "facakiroglu16" },
            //             new CustomUser() { Id = 2, Email = "ahmet@outlook.com", Password = "password", City = "Ankara", UserName = "ahmet16" },
            //                    new CustomUser() { Id = 3, Email = "mehmet@outlook.com", Password = "password", City = "Konya", UserName = "mehmet16" }

            //);

            modelBuilder.Entity<View>().HasData(
                new View() { Id = 1, Name = "ReportView", TrName="Rapor Ekranı" },
                new View() { Id = 2, Name = "RoleAndAuthorityView", TrName="Rol ve Yetki Yönetimi Ekranı" },
                new View() { Id = 3, Name = "RolesView", TrName = "Role Ekranı" },
                new View() { Id = 4, Name = "RoleAssigneView", TrName = "Rol Atama Ekranı" }
            );

            modelBuilder.Entity<ViewType>().HasData(
                new ViewType() { Id = 1, Name = "View", TrName = "Görüntüleme" },
                new ViewType() { Id = 2, Name = "Update", TrName = "Güncelleme" },
                new ViewType() { Id = 3, Name = "Create", TrName = "Ekleme" },
                new ViewType() { Id = 4, Name = "Delete", TrName = "Silme" }
            );
            
            // UserChangeLog için FK ilişkisi olmamalı (farklı DbContext'te)
            modelBuilder.Entity<UserChangeLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                // FK yok - sadece string olarak UserId tutuyoruz
            });

            // UserConsent de aynı sebeple FK'sız: Identity tabloları ApplicationDbContext'te.
            modelBuilder.Entity<UserConsent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(64);
                // Enum metin olarak saklanır: tablo hukuki bir denetim kaydı, doğrudan
                // SQL ile okunduğunda "1/2" değil "TermsOfUse/PrivacyPolicy" görünmeli.
                entity.Property(e => e.DocumentType).IsRequired().HasMaxLength(64).HasConversion<string>();
                entity.Property(e => e.DocumentVersion).IsRequired().HasMaxLength(32);
                entity.Property(e => e.ContentHash).IsRequired().HasMaxLength(64);
                entity.Property(e => e.Source).IsRequired().HasMaxLength(32);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(512);
                // "Bu kiracıdaki bu kullanıcının bu doküman için en son onayı" sorgusu.
                entity.HasIndex(e => new { e.TenantId, e.UserId, e.DocumentType, e.AcceptedAt })
                      .HasDatabaseName("IX_UserConsent_Tenant_User_Document");
            });
            
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema("ids4");
        }
    }
}
