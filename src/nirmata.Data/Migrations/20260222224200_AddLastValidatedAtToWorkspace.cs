using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nirmata.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLastValidatedAtToWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastValidatedAt",
                table: "Workspaces",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastValidatedAt",
                table: "Workspaces");
        }
    }
}
