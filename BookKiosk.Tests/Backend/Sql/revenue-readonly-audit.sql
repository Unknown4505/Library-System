-- PROPOSAL SUPPORT ONLY. Run only on an approved disposable QA database.
-- Does not create constraints, change amounts, delete rows or apply migrations.
SET NOCOUNT ON;
IF DB_NAME() NOT LIKE 'BookKiosk[_]QA[_]%'
    THROW 51000, 'Read-only audit requires a BookKiosk_QA_ disposable database.', 1;

SELECT DB_NAME() AS DatabaseName,
       DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS DatabaseCollation;

SELECT OBJECT_NAME(parent_object_id) AS TableName, name AS ConstraintName,
       definition, is_disabled, is_not_trusted
FROM sys.check_constraints
WHERE parent_object_id IN (OBJECT_ID(N'dbo.Orders'), OBJECT_ID(N'dbo.PaymentTransactions'));

SELECT OBJECT_NAME(object_id) AS TableName, name AS ColumnName,
       TYPE_NAME(user_type_id) AS SqlType, precision, scale, is_nullable
FROM sys.columns
WHERE (object_id = OBJECT_ID(N'dbo.Orders') AND name = N'TotalAmount')
   OR (object_id = OBJECT_ID(N'dbo.PaymentTransactions') AND name = N'Amount');

SELECT OrderStatus, COUNT_BIG(*) AS TotalRows,
       SUM(CASE WHEN TotalAmount <= 0 THEN CONVERT(bigint,1) ELSE 0 END) AS NonPositiveRows,
       SUM(CASE WHEN TotalAmount IS NULL THEN CONVERT(bigint,1) ELSE 0 END) AS NullAmountRows,
       SUM(CASE WHEN CompletedAt IS NULL THEN CONVERT(bigint,1) ELSE 0 END) AS MissingCompletedAtRows
FROM dbo.Orders GROUP BY OrderStatus ORDER BY OrderStatus;

SELECT COUNT_BIG(*) AS PaymentRows,
       SUM(CASE WHEN Amount <= 0 THEN CONVERT(bigint,1) ELSE 0 END) AS NonPositivePaymentRows
FROM dbo.PaymentTransactions;

-- Proposed constraint (not executed): CHECK (TotalAmount > 0).
-- Leader must decide treatment of legacy Pending/Cancelled, approve the migration,
-- compatibility plan and separate handling of reconciliation PaymentTransactions first.
