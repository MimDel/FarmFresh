using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmFresh.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTableQuaters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.CreateTable(
            //    name: "Quarters",
            //    columns: table => new
            //    {
            //        Id = table.Column<int>(type: "int", nullable: false),
            //        CityID = table.Column<int>(type: "int", nullable: true),
            //        Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
            //        NameEn = table.Column<string>(type: "nvarchar(max)", nullable: true)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_Quarters", x => x.Id);
            //        table.ForeignKey(
            //            name: "FK_Quarters_Cities_CityID",
            //            column: x => x.CityID,
            //            principalTable: "Cities",
            //            principalColumn: "Id",
            //            onDelete: ReferentialAction.Cascade);
            //    });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
