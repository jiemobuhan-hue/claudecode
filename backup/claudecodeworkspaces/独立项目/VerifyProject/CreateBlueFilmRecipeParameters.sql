-- ============================================================
-- T_BlueFilmRecipeParameters 建表 + 存储过程
-- 数据库: VisionProgram
-- 兼容: SQL Server 2008+
-- ============================================================

-- 1. 建表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'T_BlueFilmRecipeParameters')
BEGIN
    CREATE TABLE T_BlueFilmRecipeParameters (
        ParameterID             NVARCHAR(50)    NOT NULL PRIMARY KEY,
        Description             NVARCHAR(200)   NULL,
        UpdateTime              DATETIME        NULL,
        ACK                     INT             NULL,
        Enable                  INT             NOT NULL DEFAULT 1,
        ParameterName           NVARCHAR(100)   NULL,
        ParameterType           NVARCHAR(50)    NULL,
        UpperSpecificationsLimit NVARCHAR(50)   NULL,
        LowerSpecificationsLimit NVARCHAR(50)   NULL,
        Unit                    NVARCHAR(20)    NULL,
        status                  NVARCHAR(20)    NULL,
        ReserveField1           NVARCHAR(100)   NULL,
        ReserveField2           NVARCHAR(100)   NULL,
        ReserveField3           NVARCHAR(100)   NULL,
        ReserveField4           NVARCHAR(100)   NULL,
        ReserveField5           NVARCHAR(100)   NULL,
        ReserveField6           NVARCHAR(100)   NULL
    );
END
GO

-- 2. Insert 存储过程
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'Proc_InsertBlueFilmRecipeParameters') AND type = 'P')
    DROP PROCEDURE Proc_InsertBlueFilmRecipeParameters
GO
CREATE PROCEDURE Proc_InsertBlueFilmRecipeParameters
    @ParameterID                NVARCHAR(50),
    @Description                NVARCHAR(200) = NULL,
    @UpdateTime                 DATETIME = NULL,
    @ACK                        INT = NULL,
    @Enable                     INT = 1,
    @ParameterName              NVARCHAR(100) = NULL,
    @ParameterType              NVARCHAR(50) = NULL,
    @UpperSpecificationsLimit   NVARCHAR(50) = NULL,
    @LowerSpecificationsLimit   NVARCHAR(50) = NULL,
    @Unit                       NVARCHAR(20) = NULL,
    @status                     NVARCHAR(20) = NULL,
    @ReserveField1              NVARCHAR(100) = NULL,
    @ReserveField2              NVARCHAR(100) = NULL,
    @ReserveField3              NVARCHAR(100) = NULL,
    @ReserveField4              NVARCHAR(100) = NULL,
    @ReserveField5              NVARCHAR(100) = NULL,
    @ReserveField6              NVARCHAR(100) = NULL
AS
BEGIN
    INSERT INTO T_BlueFilmRecipeParameters (
        ParameterID, Description, UpdateTime, ACK, Enable,
        ParameterName, ParameterType,
        UpperSpecificationsLimit, LowerSpecificationsLimit, Unit,
        status, ReserveField1, ReserveField2, ReserveField3,
        ReserveField4, ReserveField5, ReserveField6
    ) VALUES (
        @ParameterID, @Description, @UpdateTime, @ACK, @Enable,
        @ParameterName, @ParameterType,
        @UpperSpecificationsLimit, @LowerSpecificationsLimit, @Unit,
        @status, @ReserveField1, @ReserveField2, @ReserveField3,
        @ReserveField4, @ReserveField5, @ReserveField6
    );
END
GO

-- 3. GetAll 存储过程
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'PROC_Claude_GetAllBlueFilmRecipeParameters') AND type = 'P')
    DROP PROCEDURE PROC_Claude_GetAllBlueFilmRecipeParameters
GO
CREATE PROCEDURE PROC_Claude_GetAllBlueFilmRecipeParameters
AS
BEGIN
    SELECT * FROM T_BlueFilmRecipeParameters ORDER BY ParameterID;
END
GO

-- 4. GetByParameterID 存储过程
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'PROC_Claude_GetBlueFilmRecipeParametersByParameterID') AND type = 'P')
    DROP PROCEDURE PROC_Claude_GetBlueFilmRecipeParametersByParameterID
GO
CREATE PROCEDURE PROC_Claude_GetBlueFilmRecipeParametersByParameterID
    @ParameterID NVARCHAR(50)
AS
BEGIN
    SELECT * FROM T_BlueFilmRecipeParameters WHERE ParameterID = @ParameterID;
END
GO

-- 5. Update 存储过程
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'PROC_Claude_UpdateBlueFilmRecipeParameters') AND type = 'P')
    DROP PROCEDURE PROC_Claude_UpdateBlueFilmRecipeParameters
GO
CREATE PROCEDURE PROC_Claude_UpdateBlueFilmRecipeParameters
    @ParameterID                NVARCHAR(50),
    @Description                NVARCHAR(200) = NULL,
    @UpdateTime                 DATETIME = NULL,
    @ACK                        INT = NULL,
    @Enable                     INT = 1,
    @ParameterName              NVARCHAR(100) = NULL,
    @ParameterType              NVARCHAR(50) = NULL,
    @UpperSpecificationsLimit   NVARCHAR(50) = NULL,
    @LowerSpecificationsLimit   NVARCHAR(50) = NULL,
    @Unit                       NVARCHAR(20) = NULL,
    @status                     NVARCHAR(20) = NULL,
    @ReserveField1              NVARCHAR(100) = NULL,
    @ReserveField2              NVARCHAR(100) = NULL,
    @ReserveField3              NVARCHAR(100) = NULL,
    @ReserveField4              NVARCHAR(100) = NULL,
    @ReserveField5              NVARCHAR(100) = NULL,
    @ReserveField6              NVARCHAR(100) = NULL
AS
BEGIN
    UPDATE T_BlueFilmRecipeParameters SET
        Description = @Description,
        UpdateTime = @UpdateTime,
        ACK = @ACK,
        Enable = @Enable,
        ParameterName = @ParameterName,
        ParameterType = @ParameterType,
        UpperSpecificationsLimit = @UpperSpecificationsLimit,
        LowerSpecificationsLimit = @LowerSpecificationsLimit,
        Unit = @Unit,
        status = @status,
        ReserveField1 = @ReserveField1,
        ReserveField2 = @ReserveField2,
        ReserveField3 = @ReserveField3,
        ReserveField4 = @ReserveField4,
        ReserveField5 = @ReserveField5,
        ReserveField6 = @ReserveField6
    WHERE ParameterID = @ParameterID;
END
GO

-- 6. Delete 存储过程
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'PROC_Claude_DeleteBlueFilmRecipeParameters') AND type = 'P')
    DROP PROCEDURE PROC_Claude_DeleteBlueFilmRecipeParameters
GO
CREATE PROCEDURE PROC_Claude_DeleteBlueFilmRecipeParameters
    @ParameterID NVARCHAR(50)
AS
BEGIN
    DELETE FROM T_BlueFilmRecipeParameters WHERE ParameterID = @ParameterID;
END
GO

-- 7. GetCount 存储过程
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'PROC_Claude_GetBlueFilmRecipeParametersCount') AND type = 'P')
    DROP PROCEDURE PROC_Claude_GetBlueFilmRecipeParametersCount
GO
CREATE PROCEDURE PROC_Claude_GetBlueFilmRecipeParametersCount
AS
BEGIN
    SELECT COUNT(*) FROM T_BlueFilmRecipeParameters;
END
GO
