using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiServer.Migrations
{
    public partial class ADD_INDEX_TABLE_TRANSACTION : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET @idx := (
    SELECT INDEX_NAME
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Transaction'
      AND INDEX_NAME = 'IX_Transaction_UserId_PurchaseAt'
    LIMIT 1
);
SET @sql := IF(@idx IS NULL,
    'CREATE INDEX `IX_Transaction_UserId_PurchaseAt` ON `Transaction` (`UserId`, `PurchaseAt`);',
    'SELECT 1;'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
");

            migrationBuilder.Sql(@"
SET @idx := (
    SELECT INDEX_NAME
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Transaction'
      AND INDEX_NAME = 'IX_Transaction_StoreType_StoreTransactionId'
    LIMIT 1
);
SET @sql := IF(@idx IS NULL,
    'SELECT 1;',
    'ALTER TABLE `Transaction` DROP INDEX `IX_Transaction_StoreType_StoreTransactionId`;'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
");

            migrationBuilder.Sql(@"
SET @idx := (
    SELECT INDEX_NAME
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Transaction'
      AND INDEX_NAME = 'IX_Transaction_StoreType_StoreTransactionId'
    LIMIT 1
);
SET @sql := IF(@idx IS NULL,
    'CREATE UNIQUE INDEX `IX_Transaction_StoreType_StoreTransactionId` ON `Transaction` (`StoreType`, `StoreTransactionId`);',
    'SELECT 1;'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
");

            migrationBuilder.Sql(@"
SET @idx := (
    SELECT INDEX_NAME
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Transaction'
      AND INDEX_NAME = 'IX_Transaction_UserId'
    LIMIT 1
);
SET @sql := IF(@idx IS NULL,
    'SELECT 1;',
    'ALTER TABLE `Transaction` DROP INDEX `IX_Transaction_UserId`;'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET @idx := (
    SELECT INDEX_NAME
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Transaction'
      AND INDEX_NAME = 'IX_Transaction_StoreType_StoreTransactionId'
    LIMIT 1
);
SET @sql := IF(@idx IS NULL,
    'SELECT 1;',
    'ALTER TABLE `Transaction` DROP INDEX `IX_Transaction_StoreType_StoreTransactionId`;'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
");

            migrationBuilder.Sql(@"
SET @idx := (
    SELECT INDEX_NAME
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Transaction'
      AND INDEX_NAME = 'IX_Transaction_StoreType_StoreTransactionId'
    LIMIT 1
);
SET @sql := IF(@idx IS NULL,
    'CREATE INDEX `IX_Transaction_StoreType_StoreTransactionId` ON `Transaction` (`StoreType`, `StoreTransactionId`);',
    'SELECT 1;'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
");

            migrationBuilder.Sql(@"
SET @idx := (
    SELECT INDEX_NAME
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Transaction'
      AND INDEX_NAME = 'IX_Transaction_UserId'
    LIMIT 1
);
SET @sql := IF(@idx IS NULL,
    'CREATE INDEX `IX_Transaction_UserId` ON `Transaction` (`UserId`);',
    'SELECT 1;'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
");

            migrationBuilder.Sql(@"
SET @idx := (
    SELECT INDEX_NAME
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Transaction'
      AND INDEX_NAME = 'IX_Transaction_UserId_PurchaseAt'
    LIMIT 1
);
SET @sql := IF(@idx IS NULL,
    'SELECT 1;',
    'ALTER TABLE `Transaction` DROP INDEX `IX_Transaction_UserId_PurchaseAt`;'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
");
        }
    }
}