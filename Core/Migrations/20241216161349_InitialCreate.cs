using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoryModels",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    IconFile = table.Column<string>(type: "TEXT", nullable: true),
                    Color = table.Column<string>(type: "TEXT", nullable: true),
                    IsDirectoryMath = table.Column<bool>(type: "INTEGER", nullable: false),
                    Directories = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryModels", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "WebSiteCategoryModels",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    IconFile = table.Column<string>(type: "TEXT", nullable: true),
                    Color = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebSiteCategoryModels", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "WebUrlModels",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", nullable: true),
                    IconFile = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebUrlModels", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "AppModels",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Alias = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    File = table.Column<string>(type: "TEXT", nullable: true),
                    CategoryID = table.Column<int>(type: "INTEGER", nullable: false),
                    IconFile = table.Column<string>(type: "TEXT", nullable: true),
                    TotalTime = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppModels", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "WebSiteModels",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Domain = table.Column<string>(type: "TEXT", nullable: true),
                    Alias = table.Column<string>(type: "TEXT", nullable: true),
                    CategoryID = table.Column<int>(type: "INTEGER", nullable: false),
                    IconFile = table.Column<string>(type: "TEXT", nullable: true),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebSiteModels", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DailyLogModels",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Time = table.Column<int>(type: "INTEGER", nullable: false),
                    AppModelID = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyLogModels", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HoursLogModels",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DataTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Time = table.Column<int>(type: "INTEGER", nullable: false),
                    AppModelID = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoursLogModels", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "WebBrowseLogModels",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UrlId = table.Column<int>(type: "INTEGER", nullable: false),
                    LogTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    SiteId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebBrowseLogModels", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppModels_CategoryID",
                table: "AppModels",
                column: "CategoryID");

            migrationBuilder.CreateIndex(
                name: "IX_DailyLogModels_AppModelID",
                table: "DailyLogModels",
                column: "AppModelID");

            migrationBuilder.CreateIndex(
                name: "IX_HoursLogModels_AppModelID",
                table: "HoursLogModels",
                column: "AppModelID");

            migrationBuilder.CreateIndex(
                name: "IX_WebBrowseLogModels_SiteId",
                table: "WebBrowseLogModels",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_WebBrowseLogModels_UrlId",
                table: "WebBrowseLogModels",
                column: "UrlId");

            migrationBuilder.CreateIndex(
                name: "IX_WebSiteModels_CategoryID",
                table: "WebSiteModels",
                column: "CategoryID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyLogModels");

            migrationBuilder.DropTable(
                name: "HoursLogModels");

            migrationBuilder.DropTable(
                name: "WebBrowseLogModels");

            migrationBuilder.DropTable(
                name: "AppModels");

            migrationBuilder.DropTable(
                name: "WebSiteModels");

            migrationBuilder.DropTable(
                name: "WebUrlModels");

            migrationBuilder.DropTable(
                name: "CategoryModels");

            migrationBuilder.DropTable(
                name: "WebSiteCategoryModels");
        }
    }
}
