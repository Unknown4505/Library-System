using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookKiosk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookInventoryCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [dbo].[Books]
                    WHERE [StockQuantity] < 0
                       OR [ReservedQuantity] < 0
                       OR [ReservedQuantity] > [StockQuantity])
                BEGIN
                    THROW 51000, 'Cannot add inventory constraints: Books contains invalid StockQuantity or ReservedQuantity values.', 1;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE [name] = N'CK_Books_StockQuantity'
                      AND [parent_object_id] = OBJECT_ID(N'[dbo].[Books]'))
                BEGIN
                    ALTER TABLE [dbo].[Books] WITH CHECK
                    ADD CONSTRAINT [CK_Books_StockQuantity]
                    CHECK ([StockQuantity] >= 0);
                    ALTER TABLE [dbo].[Books]
                    CHECK CONSTRAINT [CK_Books_StockQuantity];
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE [name] = N'CK_Books_ReservedQuantity'
                      AND [parent_object_id] = OBJECT_ID(N'[dbo].[Books]'))
                BEGIN
                    ALTER TABLE [dbo].[Books] WITH CHECK
                    ADD CONSTRAINT [CK_Books_ReservedQuantity]
                    CHECK ([ReservedQuantity] >= 0 AND [ReservedQuantity] <= [StockQuantity]);
                    ALTER TABLE [dbo].[Books]
                    CHECK CONSTRAINT [CK_Books_ReservedQuantity];
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE [name] = N'CK_Books_ReservedQuantity'
                      AND [parent_object_id] = OBJECT_ID(N'[dbo].[Books]'))
                BEGIN
                    ALTER TABLE [dbo].[Books]
                    DROP CONSTRAINT [CK_Books_ReservedQuantity];
                END;

                IF EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE [name] = N'CK_Books_StockQuantity'
                      AND [parent_object_id] = OBJECT_ID(N'[dbo].[Books]'))
                BEGIN
                    ALTER TABLE [dbo].[Books]
                    DROP CONSTRAINT [CK_Books_StockQuantity];
                END;
                """);
        }
    }
}
