IF DB_ID(Npkl) IS NULL CREATE DATABASE pkl;
GO
USE pkl;
GO
IF OBJECT_ID(Ndbo.pkl_coils, Nu) IS NULL
CREATE TABLE dbo.pkl_coils (
  coil_id nvarchar(64) NOT NULL PRIMARY KEY,
  entry_time_utc datetime2 NOT NULL,
  exit_time_utc datetime2 NOT NULL,
  acid_temp_c decimal(10,3) NULL,
  line_speed_mpm decimal(10,3) NULL
);
GO
MERGE dbo.pkl_coils AS t USING (SELECT N'ADV_COIL4002' AS coil_id) AS s ON t.coil_id=s.coil_id
WHEN NOT MATCHED THEN INSERT (coil_id,entry_time_utc,exit_time_utc,acid_temp_c,line_speed_mpm)
VALUES (N'ADV_COIL4002','2026-05-01T14:00:00','2026-05-01T14:35:00',82.4,185.0);
GO
