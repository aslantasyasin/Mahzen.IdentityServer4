using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityServer.Migrations.CustomDb
{
    public partial class custommig6 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UpMenuId",
                table: "View",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 22, 54, 809, DateTimeKind.Utc).AddTicks(7420));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 22, 54, 809, DateTimeKind.Utc).AddTicks(7430));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 22, 54, 809, DateTimeKind.Utc).AddTicks(7430));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 22, 54, 809, DateTimeKind.Utc).AddTicks(7430));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 22, 54, 809, DateTimeKind.Utc).AddTicks(7480));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 22, 54, 809, DateTimeKind.Utc).AddTicks(7480));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 22, 54, 809, DateTimeKind.Utc).AddTicks(7490));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 20, 10, 22, 54, 809, DateTimeKind.Utc).AddTicks(7490));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpMenuId",
                table: "View");

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
    }
}
