using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityServer.Migrations.CustomDb
{
    public partial class custommig4 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TrName",
                table: "ViewType",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ViewType",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 10, 21, 704, DateTimeKind.Utc).AddTicks(7730));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 10, 21, 704, DateTimeKind.Utc).AddTicks(7740));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 10, 21, 704, DateTimeKind.Utc).AddTicks(7740));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 10, 21, 704, DateTimeKind.Utc).AddTicks(7740));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 10, 21, 704, DateTimeKind.Utc).AddTicks(7820));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 10, 21, 704, DateTimeKind.Utc).AddTicks(7820));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 10, 21, 704, DateTimeKind.Utc).AddTicks(7820));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 10, 21, 704, DateTimeKind.Utc).AddTicks(7820));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TrName",
                table: "ViewType",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ViewType",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(1990));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(1990));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(1990));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(1990));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(2050));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(2050));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(2050));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2023, 3, 19, 18, 5, 13, 923, DateTimeKind.Utc).AddTicks(2050));
        }
    }
}
