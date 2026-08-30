using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BcReleasePlanPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: true),
                    Environments = table.Column<string>(type: "TEXT", nullable: false),
                    ModulesInUse = table.Column<string>(type: "TEXT", nullable: false),
                    AppSourceApps = table.Column<string>(type: "TEXT", nullable: false),
                    CustomExtensions = table.Column<string>(type: "TEXT", nullable: false),
                    Integrations = table.Column<string>(type: "TEXT", nullable: false),
                    Flags = table.Column<string>(type: "TEXT", nullable: false),
                    Contacts = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewCadence = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoadmapItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                    Product = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    DescriptionRaw = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: true),
                    Modules = table.Column<string>(type: "TEXT", nullable: false),
                    ChangeType = table.Column<int>(type: "INTEGER", nullable: false),
                    NeedsConfirmation = table.Column<bool>(type: "INTEGER", nullable: false),
                    TargetVersion = table.Column<string>(type: "TEXT", nullable: true),
                    PreviewDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    GaDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    EnabledBy = table.Column<int>(type: "INTEGER", nullable: false),
                    ObjectsTouched = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadHash = table.Column<string>(type: "TEXT", nullable: false),
                    SourceModifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoadmapItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReleasePlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    PeriodLabel = table.Column<string>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PublishedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentPath = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleasePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleasePlans_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoadmapItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Field = table.Column<string>(type: "TEXT", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    DetectedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Notified = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeEvents_RoadmapItems_RoadmapItemId",
                        column: x => x.RoadmapItemId,
                        principalTable: "RoadmapItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoadmapItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchScore = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchReasons = table.Column<string>(type: "TEXT", nullable: false),
                    Relevance = table.Column<int>(type: "INTEGER", nullable: false),
                    Decision = table.Column<int>(type: "INTEGER", nullable: false),
                    OverrideNote = table.Column<string>(type: "TEXT", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: true),
                    TargetWindow = table.Column<string>(type: "TEXT", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DecidedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerItems_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerItems_RoadmapItems_RoadmapItemId",
                        column: x => x.RoadmapItemId,
                        principalTable: "RoadmapItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImpactNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoadmapItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    WhyItMatters = table.Column<string>(type: "TEXT", nullable: false),
                    ActionRequired = table.Column<string>(type: "TEXT", nullable: false),
                    EffortBand = table.Column<int>(type: "INTEGER", nullable: false),
                    Risk = table.Column<int>(type: "INTEGER", nullable: false),
                    Author = table.Column<string>(type: "TEXT", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpactNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImpactNotes_RoadmapItems_RoadmapItemId",
                        column: x => x.RoadmapItemId,
                        principalTable: "RoadmapItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReleasePlanLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReleasePlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoadmapItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    ChangeType = table.Column<int>(type: "INTEGER", nullable: false),
                    GaDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EffortBand = table.Column<int>(type: "INTEGER", nullable: false),
                    Decision = table.Column<int>(type: "INTEGER", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleasePlanLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleasePlanLines_ReleasePlans_ReleasePlanId",
                        column: x => x.ReleasePlanId,
                        principalTable: "ReleasePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeEvents_RoadmapItemId_DetectedAt",
                table: "ChangeEvents",
                columns: new[] { "RoadmapItemId", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerItems_CustomerId_RoadmapItemId",
                table: "CustomerItems",
                columns: new[] { "CustomerId", "RoadmapItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerItems_RoadmapItemId",
                table: "CustomerItems",
                column: "RoadmapItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ImpactNotes_RoadmapItemId",
                table: "ImpactNotes",
                column: "RoadmapItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleasePlanLines_ReleasePlanId",
                table: "ReleasePlanLines",
                column: "ReleasePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleasePlanLines_RoadmapItemId",
                table: "ReleasePlanLines",
                column: "RoadmapItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleasePlans_CustomerId",
                table: "ReleasePlans",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_RoadmapItems_Source_ExternalId",
                table: "RoadmapItems",
                columns: new[] { "Source", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeEvents");

            migrationBuilder.DropTable(
                name: "CustomerItems");

            migrationBuilder.DropTable(
                name: "ImpactNotes");

            migrationBuilder.DropTable(
                name: "ReleasePlanLines");

            migrationBuilder.DropTable(
                name: "RoadmapItems");

            migrationBuilder.DropTable(
                name: "ReleasePlans");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
