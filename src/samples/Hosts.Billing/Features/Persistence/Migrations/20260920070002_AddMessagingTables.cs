using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hosts.Billing.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Messaging");

            migrationBuilder.CreateTable(
                name: "DelayedMessages",
                schema: "Messaging",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageTypeName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliveryTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelayedMessages", x => x.MessageId);
                });

            migrationBuilder.CreateTable(
                name: "Inbox",
                schema: "Messaging",
                columns: table => new
                {
                    MessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inbox", x => x.MessageId);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                schema: "Messaging",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageTypeName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClaimedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDispatched = table.Column<bool>(type: "bit", nullable: false),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.MessageId);
                });

            migrationBuilder.CreateTable(
                name: "Sagas",
                schema: "Messaging",
                columns: table => new
                {
                    SagaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SagaTypeName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sagas", x => x.SagaId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DelayedMessages_DeliveryTime",
                schema: "Messaging",
                table: "DelayedMessages",
                column: "DeliveryTime");

            migrationBuilder.CreateIndex(
                name: "IX_Inbox_ProcessedAt",
                schema: "Messaging",
                table: "Inbox",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_ClaimId",
                schema: "Messaging",
                table: "Outbox",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Pending",
                schema: "Messaging",
                table: "Outbox",
                columns: new[] { "IsDispatched", "ClaimedUntil", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Sagas_Correlation",
                schema: "Messaging",
                table: "Sagas",
                columns: new[] { "SagaTypeName", "CorrelationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DelayedMessages",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "Inbox",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "Outbox",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "Sagas",
                schema: "Messaging");
        }
    }
}
