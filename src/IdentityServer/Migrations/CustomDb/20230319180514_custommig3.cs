using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityServer.Migrations.CustomDb
{
    public partial class custommig3 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrName",
                table: "ViewType",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "View",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrName",
                table: "View",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedDate", "TrName" },
                values: new object[] { new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(1990), "Rapor Ekranı" });

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedDate", "TrName" },
                values: new object[] { new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(1990), "Rol ve Yetki Yönetimi Ekranı" });

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedDate", "TrName" },
                values: new object[] { new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(1990), "Role Ekranı" });

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedDate", "TrName" },
                values: new object[] { new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(1990), "Rol Atama Ekranı" });

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedDate", "TrName" },
                values: new object[] { new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(2050), "Görüntüleme" });

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedDate", "TrName" },
                values: new object[] { new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(2050), "Güncelleme" });

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedDate", "TrName" },
                values: new object[] { new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(2050), "Ekleme" });

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedDate", "TrName" },
                values: new object[] { new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(2050), "Silme" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrName",
                table: "ViewType");

            migrationBuilder.DropColumn(
                name: "TrName",
                table: "View");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "View",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 18, 13, 56, 17, 205, DateTimeKind.Utc).AddTicks(8620));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 18, 13, 56, 17, 205, DateTimeKind.Utc).AddTicks(8620));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 18, 13, 56, 17, 205, DateTimeKind.Utc).AddTicks(8620));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 18, 13, 56, 17, 205, DateTimeKind.Utc).AddTicks(8630));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 18, 13, 56, 17, 205, DateTimeKind.Utc).AddTicks(8690));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 18, 13, 56, 17, 205, DateTimeKind.Utc).AddTicks(8690));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 18, 13, 56, 17, 205, DateTimeKind.Utc).AddTicks(8690));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 18, 13, 56, 17, 205, DateTimeKind.Utc).AddTicks(8690));
        }
    }
}
