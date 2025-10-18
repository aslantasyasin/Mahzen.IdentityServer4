using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityServer.Migrations.CustomDb
{
    public partial class custommig5 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMainMenu",
                table: "View",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "View",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "View",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 12, 36, 850, DateTimeKind.Utc).AddTicks(8310));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 12, 36, 850, DateTimeKind.Utc).AddTicks(8310));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 12, 36, 850, DateTimeKind.Utc).AddTicks(8310));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 12, 36, 850, DateTimeKind.Utc).AddTicks(8310));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 12, 36, 850, DateTimeKind.Utc).AddTicks(8360));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 12, 36, 850, DateTimeKind.Utc).AddTicks(8370));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 12, 36, 850, DateTimeKind.Utc).AddTicks(8370));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 12, 36, 850, DateTimeKind.Utc).AddTicks(8370));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMainMenu",
                table: "View");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "View");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "View");

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
    }
}
