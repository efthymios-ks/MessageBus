using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessageBus.Operations.Web.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Actions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Destination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PerformedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Audits",
                columns: table => new
                {
                    MessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EndpointName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MessageTypeName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CausationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Payload = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DurationMilliseconds = table.Column<double>(type: "float", nullable: false),
                    DeliveryAttempt = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audits", x => x.MessageId);
                });

            migrationBuilder.CreateTable(
                name: "Endpoints",
                columns: table => new
                {
                    EndpointName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StaleAfter = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Endpoints", x => x.EndpointName);
                });

            migrationBuilder.CreateTable(
                name: "Failures",
                columns: table => new
                {
                    MessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EndpointName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MessageTypeName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CausationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Payload = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExceptionType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExceptionMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstFailedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastFailedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FailureCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResolvedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolutionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EditedFromMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Failures", x => x.MessageId);
                });

            migrationBuilder.CreateTable(
                name: "Instances",
                columns: table => new
                {
                    EndpointName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InstanceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MachineName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HandledMessageTypes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfigurationHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OutboxPending = table.Column<int>(type: "int", nullable: false),
                    DelayedPending = table.Column<int>(type: "int", nullable: false),
                    IsHealthy = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instances", x => new { x.EndpointName, x.InstanceId });
                    table.ForeignKey(
                        name: "FK_Instances_Endpoints_EndpointName",
                        column: x => x.EndpointName,
                        principalTable: "Endpoints",
                        principalColumn: "EndpointName",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Actions_MessageId",
                table: "Actions",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Audits_CorrelationId",
                table: "Audits",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Audits_ProcessedAt",
                table: "Audits",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Endpoints_ApiKey",
                table: "Endpoints",
                column: "ApiKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Failures_CorrelationId",
                table: "Failures",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Failures_Grouping",
                table: "Failures",
                columns: ["ExceptionType", "MessageTypeName"]);

            migrationBuilder.CreateIndex(
                name: "IX_Failures_StatusLastFailed",
                table: "Failures",
                columns: ["Status", "LastFailedAt"]);

            migrationBuilder.CreateIndex(
                name: "IX_Instances_LastSeenAt",
                table: "Instances",
                column: "LastSeenAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Actions");

            migrationBuilder.DropTable(
                name: "Audits");

            migrationBuilder.DropTable(
                name: "Failures");

            migrationBuilder.DropTable(
                name: "Instances");

            migrationBuilder.DropTable(
                name: "Endpoints");
        }
    }
}
