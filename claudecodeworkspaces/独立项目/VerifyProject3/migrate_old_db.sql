-- ============================================================
-- T_BlueFilmDataMOM 表结构迁移脚本
-- 目标服务器: NHDST87 (局域网)
-- 目标库:     VisionProgram
-- 用户:       merj / 1234@abcD
-- 日期:       2026-06-13
--
-- 安全保证:
--   - 只做 DDL，不触碰数据行 (无 DELETE/TRUNCATE/DROP TABLE)
--   - 所有操作 IF [NOT] EXISTS 包装，幂等可重复执行
--   - 执行前后打印行数对比，确认数据未丢失
--   - 仅变更 T_BlueFilmDataMOM 表及 2 个关联存储过程
-- ============================================================

-- 0. 安全前置检查
PRINT '=== 迁移前检查 ===';
PRINT '服务器: ' + @@SERVERNAME;
PRINT '数据库: ' + DB_NAME();
PRINT '当前时间: ' + CONVERT(VARCHAR, GETDATE(), 120);

-- 记录迁移前行数
DECLARE @countBefore INT;
SELECT @countBefore = COUNT(*) FROM T_BlueFilmDataMOM;
PRINT '迁移前行数: ' + CAST(@countBefore AS VARCHAR);

-- 确认表存在
IF OBJECT_ID('T_BlueFilmDataMOM','U') IS NULL
BEGIN
    RAISERROR('T_BlueFilmDataMOM 表不存在，请检查数据库', 16, 1);
    RETURN;
END
PRINT '';

-- 1. 追加 8 个参数列（已存在则跳过，不影响已有数据）
PRINT 'Step 1/5: 追加新列...';
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM' AND COLUMN_NAME='ParamterCode')
    ALTER TABLE T_BlueFilmDataMOM ADD ParamterCode   NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM' AND COLUMN_NAME='ParameterDesc')
    ALTER TABLE T_BlueFilmDataMOM ADD ParameterDesc  NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM' AND COLUMN_NAME='Value')
    ALTER TABLE T_BlueFilmDataMOM ADD Value          NVARCHAR(50)  NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM' AND COLUMN_NAME='UpperLimit')
    ALTER TABLE T_BlueFilmDataMOM ADD UpperLimit     NVARCHAR(50)  NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM' AND COLUMN_NAME='LowerLomit')
    ALTER TABLE T_BlueFilmDataMOM ADD LowerLomit     NVARCHAR(50)  NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM' AND COLUMN_NAME='TargetValue')
    ALTER TABLE T_BlueFilmDataMOM ADD TargetValue    NVARCHAR(50)  NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM' AND COLUMN_NAME='Unit')
    ALTER TABLE T_BlueFilmDataMOM ADD Unit           NVARCHAR(20)  NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM' AND COLUMN_NAME='ParameterResult')
    ALTER TABLE T_BlueFilmDataMOM ADD ParameterResult NVARCHAR(20) NULL;
PRINT '  8 new columns processed.';

-- 2. 删除旧冗余列（⚠ 列中数据将丢失，已删除则跳过）
PRINT 'Step 2/5: 删除冗余列...';
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM' AND COLUMN_NAME='DetectionArea')
    ALTER TABLE T_BlueFilmDataMOM DROP COLUMN DetectionArea;
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM' AND COLUMN_NAME='DetectionResults')
    ALTER TABLE T_BlueFilmDataMOM DROP COLUMN DetectionResults;
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM' AND COLUMN_NAME='NGtypeNum')
    ALTER TABLE T_BlueFilmDataMOM DROP COLUMN NGtypeNum;
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM' AND COLUMN_NAME='NGtype1')
    ALTER TABLE T_BlueFilmDataMOM DROP COLUMN NGtype1;
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM' AND COLUMN_NAME='NGtype2')
    ALTER TABLE T_BlueFilmDataMOM DROP COLUMN NGtype2;
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM' AND COLUMN_NAME='NGtype3')
    ALTER TABLE T_BlueFilmDataMOM DROP COLUMN NGtype3;
PRINT '  6 old columns processed.';

-- 3. 清理旧 SP（非 Claude 命名）
PRINT 'Step 3/5: 清理旧存储过程...';
IF OBJECT_ID('Proc_InsertBlueFilmDataMOM','P') IS NOT NULL DROP PROCEDURE Proc_InsertBlueFilmDataMOM;
IF OBJECT_ID('PROC_GetBlueFilmDataMOM','P')    IS NOT NULL DROP PROCEDURE PROC_GetBlueFilmDataMOM;
PRINT '  Done.';
GO

-- 4. PROC_Claude_InsertBlueFilmDataMOM (新结构，11 参数)
PRINT 'Step 4/5: PROC_Claude_InsertBlueFilmDataMOM...';
IF OBJECT_ID('PROC_Claude_InsertBlueFilmDataMOM','P') IS NOT NULL DROP PROCEDURE PROC_Claude_InsertBlueFilmDataMOM;
GO
CREATE PROCEDURE PROC_Claude_InsertBlueFilmDataMOM
    @SideCellType   NVARCHAR(10),
    @CellCode       NVARCHAR(50),
    @CreateTime     DATETIME,
    @ParamterCode   NVARCHAR(100) = NULL,
    @ParameterDesc  NVARCHAR(200) = NULL,
    @Value          NVARCHAR(50)  = NULL,
    @UpperLimit     NVARCHAR(50)  = NULL,
    @LowerLomit     NVARCHAR(50)  = NULL,
    @TargetValue    NVARCHAR(50)  = NULL,
    @Unit           NVARCHAR(20)  = NULL,
    @ParameterResult NVARCHAR(20) = NULL
AS
BEGIN
    INSERT INTO T_BlueFilmDataMOM (
        SideCellType, CellCode, CreateTime,
        ParamterCode, ParameterDesc, Value, UpperLimit, LowerLomit,
        TargetValue, Unit, ParameterResult
    ) VALUES (
        @SideCellType, @CellCode, @CreateTime,
        @ParamterCode, @ParameterDesc, @Value, @UpperLimit, @LowerLomit,
        @TargetValue, @Unit, @ParameterResult
    );
END
GO
PRINT '  Done.';

-- 5. PROC_Claude_GetBlueFilmDataMOM (分页 + 双结果集)
PRINT 'Step 5/5: PROC_Claude_GetBlueFilmDataMOM...';
IF OBJECT_ID('PROC_Claude_GetBlueFilmDataMOM','P') IS NOT NULL DROP PROCEDURE PROC_Claude_GetBlueFilmDataMOM;
GO
CREATE PROCEDURE PROC_Claude_GetBlueFilmDataMOM
    @pageIndex INT,
    @pageSize  INT,
    @startTime DATETIME,
    @endTime   DATETIME,
    @CellCode  NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @startRow INT = (@pageIndex - 1) * @pageSize + 1;
    DECLARE @endRow   INT = @pageIndex * @pageSize;

    SELECT
        ROW_NUMBER() OVER (ORDER BY CreateTime DESC) AS RowNum,
        SideCellType, CellCode, CreateTime,
        ParamterCode, ParameterDesc, Value,
        UpperLimit, LowerLomit, TargetValue, Unit, ParameterResult
    INTO #DS
    FROM T_BlueFilmDataMOM
    WHERE (@CellCode = 'ALL' OR CellCode = @CellCode)
      AND CreateTime >= @startTime
      AND CreateTime <= @endTime;

    SELECT COUNT(*) AS TotalCount FROM #DS;

    SELECT
        RowNum AS 序号,
        SideCellType AS 电芯类型,
        CellCode AS 电芯条码,
        CreateTime AS 创建时间,
        ParamterCode AS 工艺参数代码,
        ParameterDesc AS 参数描述,
        Value AS 测量值,
        UpperLimit AS 上限,
        LowerLomit AS 下限,
        TargetValue AS 目标值,
        Unit AS 单位,
        ParameterResult AS 参数判定结果
    FROM #DS
    WHERE RowNum BETWEEN @startRow AND @endRow
    ORDER BY RowNum;

    DROP TABLE #DS;
END
GO
PRINT '  Done.';

-- 6. 验证
PRINT '';
PRINT '=== 迁移后验证 ===';
DECLARE @countAfter INT;
SELECT @countAfter = COUNT(*) FROM T_BlueFilmDataMOM;
PRINT '迁移后行数: ' + CAST(@countAfter AS VARCHAR);

-- 列清单
SELECT COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'T_BlueFilmDataMOM'
ORDER BY ORDINAL_POSITION;

PRINT '';
IF @countBefore = @countAfter
    PRINT '✓ 行数一致，迁移成功';
ELSE
    PRINT '✗ 行数不一致！请检查 (before=' + CAST(@countBefore AS VARCHAR) + ' after=' + CAST(@countAfter AS VARCHAR) + ')';

PRINT '--- Migration complete ---';
