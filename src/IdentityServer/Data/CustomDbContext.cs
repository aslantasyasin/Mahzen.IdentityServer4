using System.Xml.Linq;
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
            base.OnModelCreating(modelBuilder);
        }
    }
}
