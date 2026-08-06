SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.FALCOM_MATERIAL_EVENT105_ANFORDERUNG', N'U') IS NULL
BEGIN
   CREATE TABLE dbo.FALCOM_MATERIAL_EVENT105_ANFORDERUNG
   (
      ID int IDENTITY(1,1) NOT NULL
         CONSTRAINT PK_FALCOM_MATERIAL_EVENT105_ANFORDERUNG PRIMARY KEY,
      AngefordertAm datetime2(0) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_EVENT105_ANFORDERUNG_AngefordertAm DEFAULT SYSUTCDATETIME(),
      AngefordertVon nvarchar(128) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_EVENT105_ANFORDERUNG_AngefordertVon DEFAULT N'Webseite',
      Vorgang nvarchar(30) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_EVENT105_ANFORDERUNG_Vorgang DEFAULT N'IMPORT_AUS_SPS',
      Grund nvarchar(256) NULL,
      Status nvarchar(20) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_EVENT105_ANFORDERUNG_Status DEFAULT N'OFFEN',
      BearbeitungGestartetAm datetime2(0) NULL,
      BearbeitetAm datetime2(0) NULL,
      Fehler nvarchar(1000) NULL,
      RetryCount int NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_EVENT105_ANFORDERUNG_RetryCount DEFAULT 0
   );
END;
GO

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL_EVENT105_ANFORDERUNG', N'Vorgang') IS NULL
BEGIN
   ALTER TABLE dbo.FALCOM_MATERIAL_EVENT105_ANFORDERUNG
      ADD Vorgang nvarchar(30) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_EVENT105_ANFORDERUNG_Vorgang DEFAULT N'IMPORT_AUS_SPS';
END;
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_RequestMaterialEigenschaftenEvent105
   @AngefordertVon nvarchar(128) = N'Webseite',
   @Grund nvarchar(256) = NULL,
   @AnforderungID int OUTPUT
AS
BEGIN
   SET NOCOUNT ON;

   INSERT dbo.FALCOM_MATERIAL_EVENT105_ANFORDERUNG
      (AngefordertVon, Vorgang, Grund)
   VALUES
      (COALESCE(NULLIF(LTRIM(RTRIM(@AngefordertVon)), N''), N'Webseite'), N'EXPORT_AN_SPS', @Grund);

   SET @AnforderungID = CONVERT(int, SCOPE_IDENTITY());

   SELECT AnforderungID = @AnforderungID;
END;
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_RequestMaterialEigenschaftenImportAusSps
   @AngefordertVon nvarchar(128) = N'Webseite',
   @Grund nvarchar(256) = NULL,
   @AnforderungID int OUTPUT
AS
BEGIN
   SET NOCOUNT ON;

   INSERT dbo.FALCOM_MATERIAL_EVENT105_ANFORDERUNG
      (AngefordertVon, Vorgang, Grund)
   VALUES
      (COALESCE(NULLIF(LTRIM(RTRIM(@AngefordertVon)), N''), N'Webseite'), N'IMPORT_AUS_SPS', @Grund);

   SET @AnforderungID = CONVERT(int, SCOPE_IDENTITY());

   SELECT AnforderungID = @AnforderungID;
END;
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_TryClaimMaterialEigenschaftenEvent105
AS
BEGIN
   SET NOCOUNT ON;
   SET XACT_ABORT ON;

   DECLARE @Claimed table
   (
      ID int NOT NULL,
      AngefordertAm datetime2(0) NOT NULL,
      AngefordertVon nvarchar(128) NOT NULL,
      Vorgang nvarchar(30) NOT NULL,
      Grund nvarchar(256) NULL,
      RetryCount int NOT NULL
   );

   BEGIN TRANSACTION;

   ;WITH NextRequest AS
   (
      SELECT TOP (1) *
      FROM dbo.FALCOM_MATERIAL_EVENT105_ANFORDERUNG WITH (UPDLOCK, READPAST, ROWLOCK)
      WHERE Status IN (N'OFFEN', N'FEHLER')
        AND RetryCount < 5
      ORDER BY ID
   )
   UPDATE NextRequest
      SET Status = N'IN_ARBEIT',
          BearbeitungGestartetAm = SYSUTCDATETIME(),
          Fehler = NULL,
          RetryCount = RetryCount + 1
      OUTPUT inserted.ID,
             inserted.AngefordertAm,
             inserted.AngefordertVon,
             inserted.Vorgang,
             inserted.Grund,
             inserted.RetryCount
      INTO @Claimed;

   COMMIT TRANSACTION;

   SELECT ID, AngefordertAm, AngefordertVon, Vorgang, Grund, RetryCount
   FROM @Claimed;
END;
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_CompleteMaterialEigenschaftenEvent105
   @AnforderungID int,
   @Erfolgreich bit,
   @Fehler nvarchar(1000) = NULL
AS
BEGIN
   SET NOCOUNT ON;

   UPDATE dbo.FALCOM_MATERIAL_EVENT105_ANFORDERUNG
      SET Status = CASE WHEN @Erfolgreich = 1 THEN N'ERLEDIGT' ELSE N'FEHLER' END,
          BearbeitetAm = SYSUTCDATETIME(),
          Fehler = CASE WHEN @Erfolgreich = 1 THEN NULL ELSE @Fehler END
   WHERE ID = @AnforderungID;
END;
GO
