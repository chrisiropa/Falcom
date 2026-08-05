USE [FG]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'dbo.FALCOM_KRAN_POSITION_RINGSTATUS', N'U') IS NULL
BEGIN
   CREATE TABLE dbo.FALCOM_KRAN_POSITION_RINGSTATUS
   (
      PositionID bigint NOT NULL,
      Rolle nvarchar(16) NOT NULL,
      LetzteUnterposition int NOT NULL
         CONSTRAINT DF_FALCOM_KRAN_POSITION_RINGSTATUS_LetzteUnterposition DEFAULT (0),
      ErstelltAm datetime2(3) NOT NULL
         CONSTRAINT DF_FALCOM_KRAN_POSITION_RINGSTATUS_ErstelltAm DEFAULT (SYSDATETIME()),
      GeaendertAm datetime2(3) NOT NULL
         CONSTRAINT DF_FALCOM_KRAN_POSITION_RINGSTATUS_GeaendertAm DEFAULT (SYSDATETIME()),
      CONSTRAINT PK_FALCOM_KRAN_POSITION_RINGSTATUS PRIMARY KEY (PositionID, Rolle),
      CONSTRAINT FK_FALCOM_KRAN_POSITION_RINGSTATUS_Position
         FOREIGN KEY (PositionID) REFERENCES dbo.FALCOM_KRAN_POSITION(ID),
      CONSTRAINT CK_FALCOM_KRAN_POSITION_RINGSTATUS_Rolle
         CHECK (Rolle IN (N'QUELLE', N'ZIEL')),
      CONSTRAINT CK_FALCOM_KRAN_POSITION_RINGSTATUS_LetzteUnterposition
         CHECK (LetzteUnterposition >= 0)
   );
END
GO

IF COL_LENGTH(N'dbo.FALCOM_AKTUELLE_FAHRT', N'QuelleUnterposition') IS NULL
BEGIN
   ALTER TABLE dbo.FALCOM_AKTUELLE_FAHRT
      ADD QuelleUnterposition int NULL;
END
GO

IF COL_LENGTH(N'dbo.FALCOM_AKTUELLE_FAHRT', N'ZielUnterposition') IS NULL
BEGIN
   ALTER TABLE dbo.FALCOM_AKTUELLE_FAHRT
      ADD ZielUnterposition int NULL;
END
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_GetNextKranPositionUnterposition
   @PositionID bigint,
   @Rolle nvarchar(16),
   @Unterposition int OUTPUT
AS
BEGIN
   SET NOCOUNT ON;

   SET @Unterposition = 0;

   IF @PositionID IS NULL
      RETURN;

   IF @Rolle NOT IN (N'QUELLE', N'ZIEL')
      THROW 51001, 'Ungueltige Rolle fuer FALCOM_GetNextKranPositionUnterposition.', 1;

   DECLARE @MaxUnterposition int;

   SELECT @MaxUnterposition = COUNT_BIG(*)
   FROM dbo.FALCOM_GetKranPositionAnfahrpunkte() AS p
   WHERE p.PositionID = @PositionID;

   SET @MaxUnterposition = COALESCE(@MaxUnterposition, 0);

   IF @MaxUnterposition <= 0
      RETURN;

   DECLARE @LetzteUnterposition int;

   SELECT @LetzteUnterposition = LetzteUnterposition
   FROM dbo.FALCOM_KRAN_POSITION_RINGSTATUS WITH (UPDLOCK, HOLDLOCK)
   WHERE PositionID = @PositionID
     AND Rolle = @Rolle;

   SET @Unterposition =
      CASE
         WHEN COALESCE(@LetzteUnterposition, 0) >= @MaxUnterposition THEN 1
         ELSE COALESCE(@LetzteUnterposition, 0) + 1
      END;

   IF @LetzteUnterposition IS NULL
   BEGIN
      INSERT INTO dbo.FALCOM_KRAN_POSITION_RINGSTATUS
      (
         PositionID,
         Rolle,
         LetzteUnterposition
      )
      VALUES
      (
         @PositionID,
         @Rolle,
         @Unterposition
      );
   END
   ELSE
   BEGIN
      UPDATE dbo.FALCOM_KRAN_POSITION_RINGSTATUS
         SET LetzteUnterposition = @Unterposition,
             GeaendertAm = SYSDATETIME()
       WHERE PositionID = @PositionID
         AND Rolle = @Rolle;
   END;
END;
GO
