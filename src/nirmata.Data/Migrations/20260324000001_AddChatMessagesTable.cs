using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nirmata.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    GateJson = table.Column<string>(type: "TEXT", nullable: true),
                    ArtifactsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    TimelineJson = table.Column<string>(type: "TEXT", nullable: true),
                    NextCommand = table.Column<string>(type: "TEXT", nullable: true),
                    RunId = table.Column<string>(type: "TEXT", nullable: true),
                    LogsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AgentId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_WorkspaceId_Timestamp",
                table: "ChatMessages",
                columns: new[] { "WorkspaceId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ChatMessages");
        }
    }
}
