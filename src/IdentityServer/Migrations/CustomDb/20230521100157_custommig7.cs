using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityServer.Migrations.CustomDb
{
    public partial class custommig7 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IconName",
                table: "View",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 21, 10, 1, 57, 612, DateTimeKind.Utc).AddTicks(2940));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 21, 10, 1, 57, 612, DateTimeKind.Utc).AddTicks(2950));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 21, 10, 1, 57, 612, DateTimeKind.Utc).AddTicks(2950));

            migrationBuilder.UpdateData(
                table: "View",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 21, 10, 1, 57, 612, DateTimeKind.Utc).AddTicks(2950));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 21, 10, 1, 57, 612, DateTimeKind.Utc).AddTicks(3010));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 21, 10, 1, 57, 612, DateTimeKind.Utc).AddTicks(3010));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 21, 10, 1, 57, 612, DateTimeKind.Utc).AddTicks(3010));

            migrationBuilder.UpdateData(
                table: "ViewType",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2023, 5, 21, 10, 1, 57, 612, DateTimeKind.Utc).AddTicks(3010));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IconName",
                table: "View");

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
    }
}
