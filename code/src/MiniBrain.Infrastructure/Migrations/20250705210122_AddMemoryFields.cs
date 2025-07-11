using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniBrain.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemoryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndTime",
                table: "ConversationContexts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "ConversationContexts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "ConversationContexts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "ConversationContexts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "ConversationContexts");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "ConversationContexts");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "ConversationContexts");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "ConversationContexts");
        }
    }
}
