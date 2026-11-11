using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmployeeManagement.Server.Migrations
{
    /// <inheritdoc />
    // Regjistron ne historikun Code-First datat dhe lidhjen me krijuesin e task-ut.
    // Vetem ndryshimi i nje klase C# nuk ndryshon databazen: migration duhet te aplikohet ne SQL Server.
    public partial class AddTaskAndProjectDetails : Migration
    {
        /// <inheritdoc />
        // Merr ndertuesin e veprimeve dhe shton kolonat, indeksin dhe foreign key e krijuesit.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Projects",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "EmployeeTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "EmployeeTasks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "EmployeeTasks",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTasks_CreatedByUserId",
                table: "EmployeeTasks",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeTasks_AspNetUsers_CreatedByUserId",
                table: "EmployeeTasks",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        // Kthen pas ndryshimet e Up duke hequr lidhjen dhe kolonat; nuk kthen rezultat te klienti HTTP.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeTasks_AspNetUsers_CreatedByUserId",
                table: "EmployeeTasks");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeTasks_CreatedByUserId",
                table: "EmployeeTasks");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "EmployeeTasks");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "EmployeeTasks");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "EmployeeTasks");
        }
    }
}
