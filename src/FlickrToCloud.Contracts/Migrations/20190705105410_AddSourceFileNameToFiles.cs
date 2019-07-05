using Microsoft.EntityFrameworkCore.Migrations;

namespace FlickrToCloud.Contracts.Migrations
{
    public partial class AddSourceFileNameToFiles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceFileName",
                table: "Files",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceFileName",
                table: "Files");
        }
    }
}
