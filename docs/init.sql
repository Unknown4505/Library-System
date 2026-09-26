-- ============================================================
-- BookKiosk — init.sql
-- Mô tả : DDL đầy đủ + Seed data mẫu cho SQL Server.
--          Script này MIRROR chính xác EF Core Migrations.
--          Thứ tự tạo bảng theo chiều phụ thuộc FK.
--
-- Cách dùng:
--   1. Mở SSMS / Azure Data Studio
--   2. Kết nối đến SQL Server instance của bạn
--   3. Chạy toàn bộ file này (F5)
--
-- ⚠️  LƯU Ý: Script đã idempotent (IF NOT EXISTS).
--     Có thể chạy lại nhiều lần mà không bị lỗi.
--     Seed data chỉ INSERT khi bảng đang trống.
-- ============================================================

USE master;
GO

-- Tạo database nếu chưa có
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'BookKiosk_Dev')
BEGIN
    CREATE DATABASE [BookKiosk_Dev];
END
GO

USE [BookKiosk_Dev];
GO

-- ============================================================
-- SECTION 1: TẠO BẢNG
-- Thứ tự: bảng cha trước, bảng con sau (theo FK dependency)
-- ============================================================


-- ------------------------------------------------------------
-- 1.1  Categories
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Categories')
BEGIN
    CREATE TABLE [dbo].[Categories] (
        [CategoryId]  INT            NOT NULL IDENTITY(1,1),
        [Name]        NVARCHAR(255)  NOT NULL,
        [Description] NVARCHAR(MAX)  NULL,
        [CreatedAt]   DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]   DATETIME2      NULL,

        CONSTRAINT [PK_Categories] PRIMARY KEY ([CategoryId])
    );
END
GO


-- ------------------------------------------------------------
-- 1.2  Areas  (Khu vực / Kệ sách)
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Areas')
BEGIN
    CREATE TABLE [dbo].[Areas] (
        [AreaId]         INT           NOT NULL IDENTITY(1,1),
        [Name]           NVARCHAR(255) NOT NULL,
        [MapCoordinates] VARCHAR(500)  NULL,       -- JSON {x, y} tọa độ trên UI map
        [CreatedAt]      DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]      DATETIME2     NULL,

        CONSTRAINT [PK_Areas] PRIMARY KEY ([AreaId])
    );
END
GO


-- ------------------------------------------------------------
-- 1.3  Users  (Tài khoản Admin / Nhân viên)
-- Enum UserRole: Admin=1, Staff=2
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE [dbo].[Users] (
        [UserId]       INT            NOT NULL IDENTITY(1,1),
        [Username]     VARCHAR(100)   NOT NULL,
        [PasswordHash] NVARCHAR(MAX)  NOT NULL,    -- BCrypt hash
        [FullName]     NVARCHAR(255)  NOT NULL,
        [Role]         INT            NOT NULL,    -- Enum: Admin=1, Staff=2
        [IsActive]     BIT            NOT NULL DEFAULT 1,
        [CreatedAt]    DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]    DATETIME2      NULL,

        CONSTRAINT [PK_Users] PRIMARY KEY ([UserId])
    );

    CREATE UNIQUE INDEX [IX_Users_Username] ON [dbo].[Users] ([Username]);
END
GO


-- ------------------------------------------------------------
-- 1.4  Members  (Khách hàng thành viên)
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Members')
BEGIN
    CREATE TABLE [dbo].[Members] (
        [MemberId]    INT           NOT NULL IDENTITY(1,1),
        [PhoneNumber] VARCHAR(20)   NOT NULL,
        [FullName]    NVARCHAR(255) NOT NULL,
        [Points]      INT           NOT NULL DEFAULT 0,  -- Điểm tích lũy hiện tại
        [CreatedAt]   DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]   DATETIME2     NULL,

        CONSTRAINT [PK_Members] PRIMARY KEY ([MemberId])
    );

    CREATE UNIQUE INDEX [IX_Members_PhoneNumber] ON [dbo].[Members] ([PhoneNumber]);
END
GO


-- ------------------------------------------------------------
-- 1.5  Suppliers  (Nhà cung cấp)
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Suppliers')
BEGIN
    CREATE TABLE [dbo].[Suppliers] (
        [SupplierId]  INT            NOT NULL IDENTITY(1,1),
        [Name]        NVARCHAR(255)  NOT NULL,
        [PhoneNumber] VARCHAR(20)    NULL,
        [Email]       VARCHAR(255)   NULL,
        [Address]     NVARCHAR(500)  NULL,
        [IsActive]    BIT            NOT NULL DEFAULT 1,
        [CreatedAt]   DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]   DATETIME2      NULL,

        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([SupplierId])
    );
END
GO


-- ------------------------------------------------------------
-- 1.6  Books
-- Phụ thuộc: Categories, Areas
-- Tồn kho: StockQuantity >= 0; ReservedQuantity >= 0 và <= StockQuantity
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Books')
BEGIN
    CREATE TABLE [dbo].[Books] (
        [BookId]           INT             NOT NULL IDENTITY(1,1),
        [Barcode]          VARCHAR(50)     NOT NULL,
        [Title]            NVARCHAR(255)   NOT NULL,
        [Author]           NVARCHAR(255)   NOT NULL,
        [Publisher]        NVARCHAR(255)   NULL,
        [ImageUrl]         NVARCHAR(500)   NULL,
        [CostPrice]        DECIMAL(18,0)   NOT NULL,   -- Giá vốn nhập kho
        [SellingPrice]     DECIMAL(18,0)   NOT NULL,   -- Giá bán
        [StockQuantity]    INT             NOT NULL DEFAULT 0,   -- Tồn kho thực tế
        [ReservedQuantity] INT             NOT NULL DEFAULT 0,   -- Đang giữ chỗ chờ TT
        [CategoryId]       INT             NOT NULL,
        [AreaId]           INT             NULL,       -- Nullable: sách chưa xếp kệ
        [IsActive]         BIT             NOT NULL DEFAULT 1,
        [CreatedAt]        DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]        DATETIME2       NULL,

        CONSTRAINT [PK_Books]            PRIMARY KEY ([BookId]),
        CONSTRAINT [FK_Books_Categories] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories]([CategoryId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Books_Areas]      FOREIGN KEY ([AreaId])     REFERENCES [dbo].[Areas]([AreaId])          ON DELETE SET NULL,

        -- Tồn kho không được âm; kho giữ chỗ không vượt quá tổng tồn
        CONSTRAINT [CK_Books_StockQuantity]    CHECK ([StockQuantity]    >= 0),
        CONSTRAINT [CK_Books_ReservedQuantity] CHECK ([ReservedQuantity] >= 0 AND [ReservedQuantity] <= [StockQuantity])
    );

    CREATE UNIQUE INDEX [IX_Books_Barcode] ON [dbo].[Books] ([Barcode]);
END
GO


-- ------------------------------------------------------------
-- 1.7  Kiosks
-- Phụ thuộc: Areas (nullable)
-- Enum KioskStatus: Online=1, Offline=2, Error=3
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Kiosks')
BEGIN
    CREATE TABLE [dbo].[Kiosks] (
        [KioskId]    INT           NOT NULL IDENTITY(1,1),
        [KioskName]  NVARCHAR(255) NOT NULL,
        [MacAddress] VARCHAR(50)   NOT NULL,
        [Status]     INT           NOT NULL,        -- Enum: Online=1, Offline=2, Error=3
        [LastPingAt] DATETIME2     NULL,            -- Cập nhật mỗi 30s khi heartbeat
        [AreaId]     INT           NULL,            -- Vị trí đặt máy Kiosk (nullable)
        [CreatedAt]  DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]  DATETIME2     NULL,

        CONSTRAINT [PK_Kiosks]       PRIMARY KEY ([KioskId]),
        CONSTRAINT [FK_Kiosks_Areas] FOREIGN KEY ([AreaId]) REFERENCES [dbo].[Areas]([AreaId]) ON DELETE SET NULL
    );

    CREATE UNIQUE INDEX [IX_Kiosks_MacAddress] ON [dbo].[Kiosks] ([MacAddress]);
END
GO


-- ------------------------------------------------------------
-- 1.8  KioskIncidents
-- Phụ thuộc: Kiosks
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KioskIncidents')
BEGIN
    CREATE TABLE [dbo].[KioskIncidents] (
        [IncidentId]  INT           NOT NULL IDENTITY(1,1),
        [KioskId]     INT           NOT NULL,
        [ErrorCode]   VARCHAR(50)   NOT NULL,   -- VD: "CAM_DISCONNECTED"
        [Description] NVARCHAR(MAX) NOT NULL,
        [ResolvedAt]  DATETIME2     NULL,       -- Null = chưa xử lý
        [CreatedAt]   DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]   DATETIME2     NULL,

        CONSTRAINT [PK_KioskIncidents]        PRIMARY KEY ([IncidentId]),
        CONSTRAINT [FK_KioskIncidents_Kiosks] FOREIGN KEY ([KioskId]) REFERENCES [dbo].[Kiosks]([KioskId]) ON DELETE CASCADE
    );
END
GO


-- ------------------------------------------------------------
-- 1.9  ImportReceipts  (Phiếu nhập hàng)
-- Phụ thuộc: Suppliers, Users
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ImportReceipts')
BEGIN
    CREATE TABLE [dbo].[ImportReceipts] (
        [ImportReceiptId] INT            NOT NULL IDENTITY(1,1),
        [SupplierId]      INT            NOT NULL,
        [UserId]          INT            NOT NULL,    -- Nhân viên lập phiếu
        [TotalAmount]     DECIMAL(18,0)  NOT NULL,   -- Σ(CostPrice × Quantity)
        [Note]            NVARCHAR(MAX)  NULL,
        [CreatedAt]       DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]       DATETIME2      NULL,

        CONSTRAINT [PK_ImportReceipts]           PRIMARY KEY ([ImportReceiptId]),
        CONSTRAINT [FK_ImportReceipts_Suppliers] FOREIGN KEY ([SupplierId]) REFERENCES [dbo].[Suppliers]([SupplierId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ImportReceipts_Users]     FOREIGN KEY ([UserId])     REFERENCES [dbo].[Users]([UserId])          ON DELETE NO ACTION
    );
END
GO


-- ------------------------------------------------------------
-- 1.10 ImportReceiptDetails  (Chi tiết phiếu nhập)
-- Phụ thuộc: ImportReceipts, Books
-- Composite PK: (ImportReceiptId, BookId)
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ImportReceiptDetails')
BEGIN
    CREATE TABLE [dbo].[ImportReceiptDetails] (
        [ImportReceiptId] INT           NOT NULL,
        [BookId]          INT           NOT NULL,
        [Quantity]        INT           NOT NULL,         -- Phải > 0
        [CostPrice]       DECIMAL(18,0) NOT NULL,        -- Giá vốn tại thời điểm nhập

        CONSTRAINT [PK_ImportReceiptDetails]                PRIMARY KEY ([ImportReceiptId], [BookId]),
        CONSTRAINT [FK_ImportReceiptDetails_ImportReceipts] FOREIGN KEY ([ImportReceiptId]) REFERENCES [dbo].[ImportReceipts]([ImportReceiptId]) ON DELETE CASCADE,
        CONSTRAINT [FK_ImportReceiptDetails_Books]          FOREIGN KEY ([BookId])          REFERENCES [dbo].[Books]([BookId])                    ON DELETE NO ACTION,
        CONSTRAINT [CK_ImportReceiptDetails_Quantity]       CHECK ([Quantity] > 0)
    );
END
GO


-- ------------------------------------------------------------
-- 1.11 Promotions  (Chương trình khuyến mãi — bảng mẹ)
-- Enum PromotionType: OrderDiscount=1, ProductDiscount=2
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Promotions')
BEGIN
    CREATE TABLE [dbo].[Promotions] (
        [PromotionId]   INT            NOT NULL IDENTITY(1,1),
        [Name]          NVARCHAR(255)  NOT NULL,
        [Description]   NVARCHAR(MAX)  NULL,
        [PromotionType] INT            NOT NULL,   -- Enum: OrderDiscount=1, ProductDiscount=2
        [StartDate]     DATETIME2      NOT NULL,
        [EndDate]       DATETIME2      NOT NULL,
        [IsActive]      BIT            NOT NULL DEFAULT 1,
        [CreatedAt]     DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]     DATETIME2      NULL,

        CONSTRAINT [PK_Promotions] PRIMARY KEY ([PromotionId])
    );
END
GO


-- ------------------------------------------------------------
-- 1.12 PromotionOrderDiscounts  (Chi tiết KM hóa đơn)
-- Phụ thuộc: Promotions (quan hệ 1-1, PromotionId là PK đơn)
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PromotionOrderDiscounts')
BEGIN
    CREATE TABLE [dbo].[PromotionOrderDiscounts] (
        [PromotionId]    INT           NOT NULL,
        [MinOrderValue]  DECIMAL(18,0) NOT NULL,   -- Ngưỡng SubTotal tối thiểu để áp dụng
        [DiscountAmount] DECIMAL(18,0) NOT NULL,   -- Số tiền VNĐ được giảm cố định

        CONSTRAINT [PK_PromotionOrderDiscounts]            PRIMARY KEY ([PromotionId]),
        CONSTRAINT [FK_PromotionOrderDiscounts_Promotions] FOREIGN KEY ([PromotionId]) REFERENCES [dbo].[Promotions]([PromotionId]) ON DELETE CASCADE
    );
END
GO


-- ------------------------------------------------------------
-- 1.13 PromotionProductDiscounts  (Chi tiết KM sản phẩm)
-- Phụ thuộc: Promotions, Books
-- Composite PK: (PromotionId, BookId)
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PromotionProductDiscounts')
BEGIN
    CREATE TABLE [dbo].[PromotionProductDiscounts] (
        [PromotionId]    INT           NOT NULL,
        [BookId]         INT           NOT NULL,
        [DiscountAmount] DECIMAL(18,0) NOT NULL,  -- Số tiền VNĐ giảm cho sản phẩm này

        CONSTRAINT [PK_PromotionProductDiscounts]            PRIMARY KEY ([PromotionId], [BookId]),
        CONSTRAINT [FK_PromotionProductDiscounts_Promotions] FOREIGN KEY ([PromotionId]) REFERENCES [dbo].[Promotions]([PromotionId]) ON DELETE CASCADE,
        CONSTRAINT [FK_PromotionProductDiscounts_Books]      FOREIGN KEY ([BookId])      REFERENCES [dbo].[Books]([BookId])           ON DELETE NO ACTION
    );
END
GO


-- ------------------------------------------------------------
-- 1.14 Orders
-- Phụ thuộc: Users (nullable), Members (nullable), Promotions (nullable)
-- Enum SaleChannel  : Kiosk=1, Counter=2
-- Enum OrderStatus  : Pending=1, Paid=2, Cancelled=3
-- Enum PaymentMethod: Cash=1, QR=2
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Orders')
BEGIN
    CREATE TABLE [dbo].[Orders] (
        [OrderId]        INT            NOT NULL IDENTITY(1,1),
        -- Format: ORD-yyyyMMdd-XXXX (XXXX = OrderId padding 4 chữ số)
        [OrderCode]      VARCHAR(50)    NOT NULL,
        [SaleChannel]    INT            NOT NULL,              -- Enum: Kiosk=1, Counter=2
        [OrderStatus]    INT            NOT NULL DEFAULT 1,    -- Enum: Pending=1, Paid=2, Cancelled=3
        [PaymentMethod]  INT            NOT NULL,              -- Enum: Cash=1, QR=2
        [UserId]         INT            NULL,                  -- null khi Kiosk tự phục vụ
        [MemberId]       INT            NULL,                  -- null khi khách vãng lai
        [PromotionId]    INT            NULL,                  -- null khi không áp KM
        [SubTotal]       DECIMAL(18,0)  NOT NULL,             -- Σ(LineTotal)
        [DiscountAmount] DECIMAL(18,0)  NOT NULL DEFAULT 0,   -- Tiền giảm từ KM (VNĐ)
        [PointsUsed]     INT            NOT NULL DEFAULT 0,   -- Số điểm thành viên dùng
        [TotalAmount]    DECIMAL(18,0)  NOT NULL,             -- SubTotal - DiscountAmount - (PointsUsed×1000)
        [CompletedAt]    DATETIME2      NULL,                 -- Timestamp khi xác nhận Paid
        [CreatedAt]      DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]      DATETIME2      NULL,

        CONSTRAINT [PK_Orders]            PRIMARY KEY ([OrderId]),
        CONSTRAINT [FK_Orders_Users]      FOREIGN KEY ([UserId])      REFERENCES [dbo].[Users]([UserId])           ON DELETE SET NULL,
        CONSTRAINT [FK_Orders_Members]    FOREIGN KEY ([MemberId])    REFERENCES [dbo].[Members]([MemberId])       ON DELETE SET NULL,
        CONSTRAINT [FK_Orders_Promotions] FOREIGN KEY ([PromotionId]) REFERENCES [dbo].[Promotions]([PromotionId]) ON DELETE SET NULL,
        CONSTRAINT [CK_Orders_TotalAmount] CHECK ([TotalAmount] >= 0)
    );

    CREATE UNIQUE INDEX [IX_Orders_OrderCode] ON [dbo].[Orders] ([OrderCode]);
END
GO


-- ------------------------------------------------------------
-- 1.15 OrderDetails  (Chi tiết từng dòng sách trong đơn)
-- Phụ thuộc: Orders, Books
-- Composite PK: (OrderId, BookId)
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderDetails')
BEGIN
    CREATE TABLE [dbo].[OrderDetails] (
        [OrderId]         INT           NOT NULL,
        [BookId]          INT           NOT NULL,
        -- Snapshot giá tại thời điểm mua — bảo toàn lịch sử khi giá sách thay đổi
        [UnitPriceAtTime] DECIMAL(18,0) NOT NULL,
        [Quantity]        INT           NOT NULL,        -- Phải > 0
        [LineTotal]       DECIMAL(18,0) NOT NULL,        -- = UnitPriceAtTime × Quantity

        CONSTRAINT [PK_OrderDetails]          PRIMARY KEY ([OrderId], [BookId]),
        CONSTRAINT [FK_OrderDetails_Orders]   FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders]([OrderId]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderDetails_Books]    FOREIGN KEY ([BookId])  REFERENCES [dbo].[Books]([BookId])   ON DELETE NO ACTION,
        CONSTRAINT [CK_OrderDetails_Quantity] CHECK ([Quantity] > 0)
    );
END
GO


-- ------------------------------------------------------------
-- 1.16 PaymentTransactions  (Log webhook thanh toán SePay)
-- Phụ thuộc: Orders
-- ReferenceCode UNIQUE → chống duplicate webhook (Idempotent)
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PaymentTransactions')
BEGIN
    CREATE TABLE [dbo].[PaymentTransactions] (
        [TransactionId] INT           NOT NULL IDENTITY(1,1),
        [OrderId]       INT           NOT NULL,
        [ReferenceCode] VARCHAR(50)   NOT NULL,       -- Mã tham chiếu từ SePay (UNIQUE)
        [Amount]        DECIMAL(18,0) NOT NULL,       -- Số tiền khách thực tế chuyển
        [Gateway]       VARCHAR(50)   NOT NULL,       -- VD: "SePay"
        [CreatedAt]     DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]     DATETIME2     NULL,

        CONSTRAINT [PK_PaymentTransactions]        PRIMARY KEY ([TransactionId]),
        CONSTRAINT [FK_PaymentTransactions_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders]([OrderId]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_PaymentTransactions_ReferenceCode] ON [dbo].[PaymentTransactions] ([ReferenceCode]);
END
GO


-- ------------------------------------------------------------
-- 1.17 PointTransactions  (Lịch sử tích / dùng điểm thành viên)
-- Phụ thuộc: Members, Orders (nullable)
-- Enum PointTransactionType: Earned=1, Redeemed=2
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PointTransactions')
BEGIN
    CREATE TABLE [dbo].[PointTransactions] (
        [PointTransactionId] INT           NOT NULL IDENTITY(1,1),
        [MemberId]           INT           NOT NULL,
        [OrderId]            INT           NULL,        -- Nullable: có thể điều chỉnh thủ công
        [Type]               INT           NOT NULL,    -- Enum: Earned=1, Redeemed=2
        [Points]             INT           NOT NULL,    -- Luôn dương; Type xác định chiều +/-
        [Description]        NVARCHAR(255) NULL,        -- VD: "Tích điểm đơn ORD-20260923-0001"
        [CreatedAt]          DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]          DATETIME2     NULL,

        CONSTRAINT [PK_PointTransactions]         PRIMARY KEY ([PointTransactionId]),
        CONSTRAINT [FK_PointTransactions_Members] FOREIGN KEY ([MemberId]) REFERENCES [dbo].[Members]([MemberId]) ON DELETE CASCADE,
        CONSTRAINT [FK_PointTransactions_Orders]  FOREIGN KEY ([OrderId])  REFERENCES [dbo].[Orders]([OrderId])   ON DELETE SET NULL,
        CONSTRAINT [CK_PointTransactions_Points]  CHECK ([Points] > 0)
    );
END
GO


-- ============================================================
-- SECTION 2: SEED DATA MẪU
-- Mirror chính xác DbInitializer.cs — chỉ INSERT khi trống
-- ============================================================

-- ------------------------------------------------------------
-- 2.1  Seed Categories
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[Categories])
BEGIN
    SET IDENTITY_INSERT [dbo].[Categories] ON;
    INSERT INTO [dbo].[Categories] ([CategoryId], [Name], [Description], [CreatedAt])
    VALUES
        (1, N'Sách Thiếu Nhi',      N'Sách dành cho độ tuổi thiếu nhi',     GETUTCDATE()),
        (2, N'Công Nghệ Thông Tin', N'Lập trình, mạng máy tính, phần mềm', GETUTCDATE()),
        (3, N'Văn Học',             N'Tiểu thuyết, truyện ngắn, tản văn',   GETUTCDATE());
    SET IDENTITY_INSERT [dbo].[Categories] OFF;
END
GO


-- ------------------------------------------------------------
-- 2.2  Seed Areas
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[Areas])
BEGIN
    SET IDENTITY_INSERT [dbo].[Areas] ON;
    INSERT INTO [dbo].[Areas] ([AreaId], [Name], [CreatedAt])
    VALUES
        (1, N'Kệ A1 - Tầng 1', GETUTCDATE()),
        (2, N'Kệ B2 - Tầng 2', GETUTCDATE());
    SET IDENTITY_INSERT [dbo].[Areas] OFF;
END
GO


-- ------------------------------------------------------------
-- 2.3  Seed Users  (Admin mặc định — password: admin123)
-- PasswordHash = BCrypt hash của chuỗi "admin123"
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[Users])
BEGIN
    SET IDENTITY_INSERT [dbo].[Users] ON;
    INSERT INTO [dbo].[Users] ([UserId], [Username], [PasswordHash], [FullName], [Role], [IsActive], [CreatedAt])
    VALUES
        (1, 'admin',
         '$2a$11$0.mYpM.2n9FzR/VfU.5y5eF2FwFk3ZfR5eT5vC5hQ/kS9wP9nU8Uq',
         N'Administrator', 1, 1, GETUTCDATE());
    SET IDENTITY_INSERT [dbo].[Users] OFF;
END
GO


-- ------------------------------------------------------------
-- 2.4  Seed Books
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[Books])
BEGIN
    SET IDENTITY_INSERT [dbo].[Books] ON;
    INSERT INTO [dbo].[Books]
        ([BookId], [Barcode], [Title], [Author],
         [CostPrice], [SellingPrice], [StockQuantity], [ReservedQuantity],
         [CategoryId], [AreaId], [IsActive], [CreatedAt])
    VALUES
        -- Sách Thiếu Nhi — còn hàng
        (1, '8935244878235', N'Dế Mèn Phiêu Lưu Ký', N'Tô Hoài',
         30000, 50000, 20, 0, 1, 1, 1, GETUTCDATE()),

        -- Công Nghệ — còn hàng, có 1 quyển đang được giữ chỗ
        (2, '9780132350884', N'Clean Code', N'Robert C. Martin',
         200000, 350000, 5, 1, 2, 2, 1, GETUTCDATE()),

        -- Văn Học — HẾT HÀNG (test case)
        (3, '8936049520011', N'Số Đỏ', N'Vũ Trọng Phụng',
         45000, 80000, 0, 0, 3, 1, 1, GETUTCDATE());
    SET IDENTITY_INSERT [dbo].[Books] OFF;
END
GO


-- ------------------------------------------------------------
-- 2.5  Seed Kiosks
-- Enum KioskStatus: Online=1
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[Kiosks])
BEGIN
    SET IDENTITY_INSERT [dbo].[Kiosks] ON;
    INSERT INTO [dbo].[Kiosks] ([KioskId], [KioskName], [MacAddress], [Status], [LastPingAt], [AreaId], [CreatedAt])
    VALUES
        (1, N'Kiosk Tầng 1 - Sảnh chính', '00-14-22-01-23-45',
         1, GETUTCDATE(), 1, GETUTCDATE());
    SET IDENTITY_INSERT [dbo].[Kiosks] OFF;
END
GO


-- ------------------------------------------------------------
-- 2.6  Seed Promotions + PromotionOrderDiscounts
-- Enum PromotionType: OrderDiscount=1
-- Chương trình: Giảm 20K cho hóa đơn từ 100K
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[Promotions])
BEGIN
    SET IDENTITY_INSERT [dbo].[Promotions] ON;
    INSERT INTO [dbo].[Promotions]
        ([PromotionId], [Name], [Description], [PromotionType],
         [StartDate], [EndDate], [IsActive], [CreatedAt])
    VALUES
        (1, N'Khai trương Kiosk', N'Giảm 20K cho hóa đơn từ 100K',
         1,                              -- PromotionType = OrderDiscount
         DATEADD(DAY, -1, GETUTCDATE()), -- StartDate = hôm qua
         DATEADD(MONTH, 1, GETUTCDATE()),-- EndDate   = 1 tháng sau
         1, GETUTCDATE());
    SET IDENTITY_INSERT [dbo].[Promotions] OFF;

    INSERT INTO [dbo].[PromotionOrderDiscounts] ([PromotionId], [MinOrderValue], [DiscountAmount])
    VALUES (1, 100000, 20000);
END
GO


-- ============================================================
-- SECTION 3: KIỂM TRA KẾT QUẢ
-- Kết quả mong đợi: Categories=3, Areas=2, Users=1, Books=3,
--                   Kiosks=1, Promotions=1, PromotionOrderDiscounts=1,
--                   các bảng còn lại = 0 (chưa có data)
-- ============================================================
SELECT
    'Categories'               AS [Table], COUNT(*) AS [Rows] FROM [dbo].[Categories]
UNION ALL SELECT 'Areas',                  COUNT(*) FROM [dbo].[Areas]
UNION ALL SELECT 'Users',                  COUNT(*) FROM [dbo].[Users]
UNION ALL SELECT 'Books',                  COUNT(*) FROM [dbo].[Books]
UNION ALL SELECT 'Kiosks',                 COUNT(*) FROM [dbo].[Kiosks]
UNION ALL SELECT 'KioskIncidents',         COUNT(*) FROM [dbo].[KioskIncidents]
UNION ALL SELECT 'Suppliers',              COUNT(*) FROM [dbo].[Suppliers]
UNION ALL SELECT 'ImportReceipts',         COUNT(*) FROM [dbo].[ImportReceipts]
UNION ALL SELECT 'ImportReceiptDetails',   COUNT(*) FROM [dbo].[ImportReceiptDetails]
UNION ALL SELECT 'Members',                COUNT(*) FROM [dbo].[Members]
UNION ALL SELECT 'PointTransactions',      COUNT(*) FROM [dbo].[PointTransactions]
UNION ALL SELECT 'Promotions',             COUNT(*) FROM [dbo].[Promotions]
UNION ALL SELECT 'PromotionOrderDiscounts',    COUNT(*) FROM [dbo].[PromotionOrderDiscounts]
UNION ALL SELECT 'PromotionProductDiscounts',  COUNT(*) FROM [dbo].[PromotionProductDiscounts]
UNION ALL SELECT 'Orders',                 COUNT(*) FROM [dbo].[Orders]
UNION ALL SELECT 'OrderDetails',           COUNT(*) FROM [dbo].[OrderDetails]
UNION ALL SELECT 'PaymentTransactions',    COUNT(*) FROM [dbo].[PaymentTransactions]
ORDER BY [Table];
GO
