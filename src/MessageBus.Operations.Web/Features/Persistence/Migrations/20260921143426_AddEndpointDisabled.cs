using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessageBus.Operations.Web.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEndpointDisabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Disabled",
                table: "Endpoints",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Disabled",
                table: "Endpoints");
        }
    }
}
