IF OBJECT_ID(N'dbo.adminNarrativeMessage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.adminNarrativeMessage
    (
        id BIGINT IDENTITY(1,1) NOT NULL,
        userId INT NOT NULL,
        gameId INT NOT NULL,
        category NVARCHAR(50) NOT NULL,
        firstId NVARCHAR(450) NOT NULL,
        secondId NVARCHAR(450) NOT NULL,
        explanationId NVARCHAR(450) NULL,
        narTextsJson NVARCHAR(MAX) NOT NULL,
        createdAtUtc DATETIME2 NOT NULL,
        deliveredAtUtc DATETIME2 NULL,
        seenAtUtc DATETIME2 NULL,
        cancelled BIT NOT NULL CONSTRAINT DF_adminNarrativeMessage_cancelled DEFAULT (0),
        CONSTRAINT PK_adminNarrativeMessage PRIMARY KEY CLUSTERED (id),
        CONSTRAINT FK_adminNarrativeMessage_user FOREIGN KEY (userId) REFERENCES dbo.[user](id)
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_adminNarrativeMessage_pending' AND object_id = OBJECT_ID(N'dbo.adminNarrativeMessage'))
    CREATE INDEX IX_adminNarrativeMessage_pending ON dbo.adminNarrativeMessage(userId, cancelled, deliveredAtUtc, id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_adminNarrativeMessage_scope' AND object_id = OBJECT_ID(N'dbo.adminNarrativeMessage'))
    CREATE INDEX IX_adminNarrativeMessage_scope ON dbo.adminNarrativeMessage(userId, gameId, category, id);
GO
