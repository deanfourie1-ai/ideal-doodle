using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BcReleasePlanPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReleasePlanLineDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerNote",
                table: "ReleasePlanLines",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Risk",
                table: "ReleasePlanLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TargetVersion",
                table: "ReleasePlanLines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhyItMatters",
                table: "ReleasePlanLines",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerNote",
                table: "ReleasePlanLines");

            migrationBuilder.DropColumn(
                name: "Risk",
                table: "ReleasePlanLines");

            migrationBuilder.DropColumn(
                name: "TargetVersion",
                table: "ReleasePlanLines");

            migrationBuilder.DropColumn(
                name: "WhyItMatters",
                table: "ReleasePlanLines");
        }
    }
}
