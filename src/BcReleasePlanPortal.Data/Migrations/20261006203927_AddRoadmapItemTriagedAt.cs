using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BcReleasePlanPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoadmapItemTriagedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TriagedAt",
                table: "RoadmapItems",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TriagedAt",
                table: "RoadmapItems");
        }
    }
}
