-- =====================================================
-- VisionProgram 数据库存储过程脚本
-- 适用于 T_BlueFilmDetection 和 T_HarnessMeasure 表
-- =====================================================

USE VisionProgram;
GO

-- =====================================================
-- T_BlueFilmDetection 存储过程
-- =====================================================

-- 创建蓝膜检测表（如果不存在）
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'T_BlueFilmDetection')
BEGIN
    CREATE TABLE [dbo].[T_BlueFilmDetection](
        [Id] [bigint] IDENTITY(1,1) NOT NULL,
        [CellCode] [nvarchar](50) NULL,
        [DetectionTime] [datetime] NOT NULL,
        [DetectionResult] [nvarchar](20) NULL,
        [NgType] [nvarchar](50) NULL,
        [NgPosition] [nvarchar](100) NULL,
        [NgArea] [float] NULL,
        [CameraId] [nvarchar](20) NULL,
        [Operator] [nvarchar](20) NULL,
        [Remark] [nvarchar](200) NULL,
        [CreateTime] [datetime] NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_T_BlueFilmDetection] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- 插入蓝膜检测记录
CREATE OR ALTER PROCEDURE [dbo].[PROC_InsertBlueFilmDetection]
    @CellCode NVARCHAR(50),
    @DetectionTime DATETIME,
    @DetectionResult NVARCHAR(20),
    @NgType NVARCHAR(50),
    @NgPosition NVARCHAR(100),
    @NgArea FLOAT,
    @CameraId NVARCHAR(20),
    @Operator NVARCHAR(20),
    @Remark NVARCHAR(200),
    @CreateTime DATETIME
AS
BEGIN
    INSERT INTO T_BlueFilmDetection (CellCode, DetectionTime, DetectionResult, NgType, NgPosition, NgArea, CameraId, Operator, Remark, CreateTime)
    VALUES (@CellCode, @DetectionTime, @DetectionResult, @NgType, @NgPosition, @NgArea, @CameraId, @Operator, @Remark, @CreateTime);
END
GO

-- 更新蓝膜检测记录
CREATE OR ALTER PROCEDURE [dbo].[PROC_UpdateBlueFilmDetection]
    @Id BIGINT,
    @CellCode NVARCHAR(50),
    @DetectionTime DATETIME,
    @DetectionResult NVARCHAR(20),
    @NgType NVARCHAR(50),
    @NgPosition NVARCHAR(100),
    @NgArea FLOAT,
    @CameraId NVARCHAR(20),
    @Operator NVARCHAR(20),
    @Remark NVARCHAR(200)
AS
BEGIN
    UPDATE T_BlueFilmDetection
    SET CellCode = @CellCode,
        DetectionTime = @DetectionTime,
        DetectionResult = @DetectionResult,
        NgType = @NgType,
        NgPosition = @NgPosition,
        NgArea = @NgArea,
        CameraId = @CameraId,
        Operator = @Operator,
        Remark = @Remark
    WHERE Id = @Id;
END
GO

-- 删除蓝膜检测记录
CREATE OR ALTER PROCEDURE [dbo].[PROC_DeleteBlueFilmDetection]
    @Id BIGINT
AS
BEGIN
    DELETE FROM T_BlueFilmDetection WHERE Id = @Id;
END
GO

-- 根据ID查询蓝膜检测记录
CREATE OR ALTER PROCEDURE [dbo].[PROC_GetBlueFilmDetectionById]
    @Id BIGINT
AS
BEGIN
    SELECT * FROM T_BlueFilmDetection WHERE Id = @Id;
END
GO

-- 查询所有蓝膜检测记录
CREATE OR ALTER PROCEDURE [dbo].[PROC_GetAllBlueFilmDetection]
AS
BEGIN
    SELECT * FROM T_BlueFilmDetection ORDER BY Id DESC;
END
GO

-- 分页查询蓝膜检测记录
CREATE OR ALTER PROCEDURE [dbo].[PROC_GetBlueFilmDetectionByPage]
    @PageIndex INT,
    @PageSize INT,
    @CellCode NVARCHAR(50) = NULL,
    @StartTime DATETIME = NULL,
    @EndTime DATETIME = NULL
AS
BEGIN
    -- 获取总数
    SELECT COUNT(1) FROM T_BlueFilmDetection
    WHERE (@CellCode IS NULL OR CellCode = @CellCode)
      AND (@StartTime IS NULL OR DetectionTime >= @StartTime)
      AND (@EndTime IS NULL OR DetectionTime <= @EndTime);

    -- 分页数据
    SELECT * FROM T_BlueFilmDetection
    WHERE (@CellCode IS NULL OR CellCode = @CellCode)
      AND (@StartTime IS NULL OR DetectionTime >= @StartTime)
      AND (@EndTime IS NULL OR DetectionTime <= @EndTime)
    ORDER BY Id DESC
    OFFSET (@PageIndex - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- 获取蓝膜检测记录总数
CREATE OR ALTER PROCEDURE [dbo].[PROC_GetBlueFilmDetectionCount]
    @CellCode NVARCHAR(50) = NULL,
    @StartTime DATETIME = NULL,
    @EndTime DATETIME = NULL
AS
BEGIN
    SELECT COUNT(1) FROM T_BlueFilmDetection
    WHERE (@CellCode IS NULL OR CellCode = @CellCode)
      AND (@StartTime IS NULL OR DetectionTime >= @StartTime)
      AND (@EndTime IS NULL OR DetectionTime <= @EndTime);
END
GO

-- 根据电芯码查询蓝膜检测记录
CREATE OR ALTER PROCEDURE [dbo].[PROC_GetBlueFilmDetectionByCellCode]
    @CellCode NVARCHAR(50)
AS
BEGIN
    SELECT * FROM T_BlueFilmDetection WHERE CellCode = @CellCode ORDER BY DetectionTime DESC;
END
GO

-- =====================================================
-- T_HarnessMeasure 存储过程
-- =====================================================

-- 创建线束测量表（如果不存在）
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'T_HarnessMeasure')
BEGIN
    CREATE TABLE [dbo].[T_HarnessMeasure](
        [Id] [bigint] IDENTITY(1,1) NOT NULL,
        [HarnessCode] [nvarchar](50) NULL,
        [MeasureTime] [datetime] NOT NULL,
        [Length] [float] NULL,
        [Width] [float] NULL,
        [Height] [float] NULL,
        [MeasureResult] [nvarchar](20) NULL,
        [Tolerance] [nvarchar](50) NULL,
        [StationId] [nvarchar](20) NULL,
        [Operator] [nvarchar](20) NULL,
        [Remark] [nvarchar](200) NULL,
        [CreateTime] [datetime] NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_T_HarnessMeasure] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- 插入线束测量记录
CREATE OR ALTER PROCEDURE [dbo].[PROC_InsertHarnessMeasure]
    @HarnessCode NVARCHAR(50),
    @MeasureTime DATETIME,
    @Length FLOAT,
    @Width FLOAT,
    @Height FLOAT,
    @MeasureResult NVARCHAR(20),
    @Tolerance NVARCHAR(50),
    @StationId NVARCHAR(20),
    @Operator NVARCHAR(20),
    @Remark NVARCHAR(200),
    @CreateTime DATETIME
AS
BEGIN
    INSERT INTO T_HarnessMeasure (HarnessCode, MeasureTime, Length, Width, Height, MeasureResult, Tolerance, StationId, Operator, Remark, CreateTime)
    VALUES (@HarnessCode, @MeasureTime, @Length, @Width, @Height, @MeasureResult, @Tolerance, @StationId, @Operator, @Remark, @CreateTime);
END
GO

-- 更新线束测量记录
CREATE OR ALTER PROCEDURE [dbo].[PROC_UpdateHarnessMeasure]
    @Id BIGINT,
    @HarnessCode NVARCHAR(50),
    @MeasureTime DATETIME,
    @Length FLOAT,
    @Width FLOAT,
    @Height FLOAT,
    @MeasureResult NVARCHAR(20),
    @Tolerance NVARCHAR(50),
    @StationId NVARCHAR(20),
    @Operator NVARCHAR(20),
    @Remark NVARCHAR(200)
AS
BEGIN
    UPDATE T_HarnessMeasure
    SET HarnessCode = @HarnessCode,
        MeasureTime = @MeasureTime,
        Length = @Length,
        Width = @Width,
        Height = @Height,
        MeasureResult = @MeasureResult,
        Tolerance = @Tolerance,
        StationId = @StationId,
        Operator = @Operator,
        Remark = @Remark
    WHERE Id = @Id;
END
GO

-- 删除线束测量记录
CREATE OR ALTER PROCEDURE [dbo].[PROC_DeleteHarnessMeasure]
    @Id BIGINT
AS
BEGIN
    DELETE FROM T_HarnessMeasure WHERE Id = @Id;
END
GO

-- 根据ID查询线束测量记录
CREATE OR ALTER PROCEDURE [dbo].[PROC_GetHarnessMeasureById]
    @Id BIGINT
AS
BEGIN
    SELECT * FROM T_HarnessMeasure WHERE Id = @Id;
END
GO

-- 查询所有线束测量记录
CREATE OR ALTER PROCEDURE [dbo].[PROC_GetAllHarnessMeasure]
AS
BEGIN
    SELECT * FROM T_HarnessMeasure ORDER BY Id DESC;
END
GO

-- 分页查询线束测量记录
CREATE OR ALTER PROCEDURE [dbo].[PROC_GetHarnessMeasureByPage]
    @PageIndex INT,
    @PageSize INT,
    @HarnessCode NVARCHAR(50) = NULL,
    @StartTime DATETIME = NULL,
    @EndTime DATETIME = NULL
AS
BEGIN
    -- 获取总数
    SELECT COUNT(1) FROM T_HarnessMeasure
    WHERE (@HarnessCode IS NULL OR HarnessCode = @HarnessCode)
      AND (@StartTime IS NULL OR MeasureTime >= @StartTime)
      AND (@EndTime IS NULL OR MeasureTime <= @EndTime);

    -- 分页数据
    SELECT * FROM T_HarnessMeasure
    WHERE (@HarnessCode IS NULL OR HarnessCode = @HarnessCode)
      AND (@StartTime IS NULL OR MeasureTime >= @StartTime)
      AND (@EndTime IS NULL OR MeasureTime <= @EndTime)
    ORDER BY Id DESC
    OFFSET (@PageIndex - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- 获取线束测量记录总数
CREATE OR ALTER PROCEDURE [dbo].[PROC_GetHarnessMeasureCount]
    @HarnessCode NVARCHAR(50) = NULL,
    @StartTime DATETIME = NULL,
    @EndTime DATETIME = NULL
AS
BEGIN
    SELECT COUNT(1) FROM T_HarnessMeasure
    WHERE (@HarnessCode IS NULL OR HarnessCode = @HarnessCode)
      AND (@StartTime IS NULL OR MeasureTime >= @StartTime)
      AND (@EndTime IS NULL OR MeasureTime <= @EndTime);
END
GO

-- 根据线束码查询线束测量记录
CREATE OR ALTER PROCEDURE [dbo].[PROC_GetHarnessMeasureByHarnessCode]
    @HarnessCode NVARCHAR(50)
AS
BEGIN
    SELECT * FROM T_HarnessMeasure WHERE HarnessCode = @HarnessCode ORDER BY MeasureTime DESC;
END
GO
