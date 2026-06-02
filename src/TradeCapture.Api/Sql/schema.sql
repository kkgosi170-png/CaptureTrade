-- Idempotent schema. Safe to run repeatedly (guards on object existence).

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CurrencyRates' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.CurrencyRates
    (
        FromCurrency char(3)       NOT NULL,
        ToCurrency   char(3)       NOT NULL,
        Rate         decimal(18,6) NOT NULL,
        AsOf         date          NOT NULL,
        CONSTRAINT PK_CurrencyRates PRIMARY KEY (FromCurrency, ToCurrency, AsOf)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Trades' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.Trades
    (
        TradeId      bigint IDENTITY(1,1) NOT NULL,
        ExternalId   varchar(64)   NOT NULL,
        Account      varchar(64)   NOT NULL,
        Symbol       varchar(32)   NOT NULL,
        Side         varchar(4)    NOT NULL,
        Quantity     decimal(18,4) NOT NULL,
        Price        decimal(18,4) NOT NULL,
        Currency     char(3)       NOT NULL,
        TradeTime    datetime2(3)  NOT NULL,
        Notional     decimal(18,4) NOT NULL,
        NotionalBase decimal(18,4) NOT NULL,
        BaseCurrency char(3)       NOT NULL,
        RateUsed     decimal(18,6) NOT NULL,
        RateAsOf     date          NOT NULL,
        CreatedAtUtc datetime2(3)  NOT NULL
            CONSTRAINT DF_Trades_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_Trades            PRIMARY KEY (TradeId),
        -- Idempotency is enforced here: at most one row per external id, ever.
        CONSTRAINT UQ_Trades_ExternalId UNIQUE (ExternalId),
        CONSTRAINT CK_Trades_Side       CHECK (Side IN ('BUY', 'SELL')),
        CONSTRAINT CK_Trades_Quantity   CHECK (Quantity > 0),
        CONSTRAINT CK_Trades_Price      CHECK (Price > 0)
    );

    -- Supports the report's date-range scan + grouping.
    CREATE INDEX IX_Trades_TradeTime ON dbo.Trades (TradeTime) INCLUDE (Account, Symbol);
END;
