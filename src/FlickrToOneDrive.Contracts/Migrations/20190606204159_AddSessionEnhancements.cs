using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FlickrToOneDrive.Contracts.Migrations
{
    public partial class AddSessionEnhancements : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DestinationCloud = table.Column<string>(nullable: true),
                    SourceCloud = table.Column<string>(nullable: true),
                    DestinationFolder = table.Column<string>(nullable: true),
                    Started = table.Column<DateTime>(nullable: false),
                    Mode = table.Column<int>(nullable: false),
                    FilesOrigin = table.Column<int>(nullable: false),
                    State = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Files",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(nullable: false),
                    SourceUrl = table.Column<string>(nullable: true),
                    SourcePath = table.Column<string>(nullable: true),
                    FileName = table.Column<string>(nullable: true),
                    MonitorUrl = table.Column<string>(nullable: true),
                    ResponseData = table.Column<string>(nullable: true),
                    State = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Files_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Files_SessionId",
                table: "Files",
                column: "SessionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Files");

            migrationBuilder.DropTable(
                name: "Sessions");
        }
    }
}
