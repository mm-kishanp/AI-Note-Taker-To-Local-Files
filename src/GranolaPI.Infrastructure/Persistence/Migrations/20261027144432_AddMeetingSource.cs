using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GranolaPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Meetings",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "Granola");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "Meetings");
        }
    }
}
