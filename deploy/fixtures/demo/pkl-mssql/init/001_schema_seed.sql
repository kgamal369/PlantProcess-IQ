IF DB_ID(N'pkl') IS NULL
BEGIN
    CREATE DATABASE pkl;
END
GO

USE pkl;
GO

IF OBJECT_ID(N'dbo.pkl_coils', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pkl_coils
    (
        coil_id nvarchar(64) NOT NULL PRIMARY KEY,
        entry_time_utc datetime2 NOT NULL,
        exit_time_utc datetime2 NOT NULL,
        acid_temp_c decimal(10,3) NULL,
        line_speed_mpm decimal(10,3) NULL
    );
END
GO

;WITH n AS
(
    SELECT TOP (5600)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS rn
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
),
generated AS
(
    SELECT
        CONCAT(N'C-', RIGHT(CONCAT(N'0000000', CAST(4400000 + rn AS nvarchar(20))), 7)) AS coil_id,
        DATEADD(minute, rn * 7, CAST('2026-05-01T00:00:00' AS datetime2)) AS entry_time_utc,
        DATEADD(minute, rn * 7 + 35, CAST('2026-05-01T00:00:00' AS datetime2)) AS exit_time_utc,
        CAST(78.0 + (rn % 12) AS decimal(10,3)) AS acid_temp_c,
        CAST(165.0 + (rn % 40) AS decimal(10,3)) AS line_speed_mpm
    FROM n
    UNION ALL
    SELECT
        N'C-0044170',
        CAST('2026-05-14T14:00:00' AS datetime2),
        CAST('2026-05-14T14:35:00' AS datetime2),
        CAST(82.4 AS decimal(10,3)),
        CAST(185.0 AS decimal(10,3))
)
MERGE dbo.pkl_coils AS t
USING generated AS s
ON t.coil_id = s.coil_id
WHEN MATCHED THEN UPDATE SET
    entry_time_utc = s.entry_time_utc,
    exit_time_utc = s.exit_time_utc,
    acid_temp_c = s.acid_temp_c,
    line_speed_mpm = s.line_speed_mpm
WHEN NOT MATCHED THEN INSERT
    (coil_id, entry_time_utc, exit_time_utc, acid_temp_c, line_speed_mpm)
VALUES
    (s.coil_id, s.entry_time_utc, s.exit_time_utc, s.acid_temp_c, s.line_speed_mpm);
GO