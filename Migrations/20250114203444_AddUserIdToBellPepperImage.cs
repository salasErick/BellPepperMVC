using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellPepperMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToBellPepperImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PredictedMaturityLevel",
                table: "BellPepperImages",
                type: "nvarchar(50)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)");

            migrationBuilder.AddColumn<bool>(
                name: "HasDetailedAnalysis",
                table: "BellPepperImages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "InverseFFT",
                table: "BellPepperImages",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxValue",
                table: "BellPepperImages",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MeanValue",
                table: "BellPepperImages",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MedianValue",
                table: "BellPepperImages",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinValue",
                table: "BellPepperImages",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PredictionConfidence",
                table: "BellPepperImages",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<byte[]>(
                name: "ProcessedImage",
                table: "BellPepperImages",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SobelH1",
                table: "BellPepperImages",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SobelH2",
                table: "BellPepperImages",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SpectrumB",
                table: "BellPepperImages",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SpectrumCombined",
                table: "BellPepperImages",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SpectrumG",
                table: "BellPepperImages",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SpectrumR",
                table: "BellPepperImages",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StdValue",
                table: "BellPepperImages",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "BellPepperImages",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_BellPepperImages_UserId",
                table: "BellPepperImages",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BellPepperImages_AspNetUsers_UserId",
                table: "BellPepperImages",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BellPepperImages_AspNetUsers_UserId",
                table: "BellPepperImages");

            migrationBuilder.DropIndex(
                name: "IX_BellPepperImages_UserId",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "HasDetailedAnalysis",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "InverseFFT",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "MaxValue",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "MeanValue",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "MedianValue",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "MinValue",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "PredictionConfidence",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "ProcessedImage",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "SobelH1",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "SobelH2",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "SpectrumB",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "SpectrumCombined",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "SpectrumG",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "SpectrumR",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "StdValue",
                table: "BellPepperImages");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "BellPepperImages");

            migrationBuilder.AlterColumn<string>(
                name: "PredictedMaturityLevel",
                table: "BellPepperImages",
                type: "nvarchar(100)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)");
        }
    }
}
