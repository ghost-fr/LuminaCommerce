using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lumina.Infrastructure.Migrations;

/// <summary>
/// Adds TicketSequences table for atomic gap-free ticket numbering per store.
/// From Claude Drive patch claude-fix-ticket-sequence.zip (2026-09-10).
/// </summary>
public partial class AddTicketSequences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TicketSequences",
            columns: table => new
            {
                StoreId = table.Column<Guid>(type: "TEXT", nullable: false),
                NextNumber = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TicketSequences", x => x.StoreId);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TicketSequences");
    }
}
