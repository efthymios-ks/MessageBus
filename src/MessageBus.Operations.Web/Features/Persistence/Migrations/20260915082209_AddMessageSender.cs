using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessageBus.Operations.Web.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageSender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SentBy",
                table: "Failures",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SentBy",
                table: "Audits",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SentBy",
                table: "Failures");

            migrationBuilder.DropColumn(
                name: "SentBy",
                table: "Audits");
        }
    }
}
