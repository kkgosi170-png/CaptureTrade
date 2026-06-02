-- Idempotent seed. Safe to run repeatedly.

-- Reference rates to the base currency (USD). The stub "SOAP" service reads these.
MERGE dbo.CurrencyRates AS target
USING (VALUES
    ('USD', 'USD', 1.000000, '2025-01-01'),
    ('EUR', 'USD', 1.090000, '2025-01-15'),
    ('GBP', 'USD', 1.270000, '2025-01-15'),
    ('ZAR', 'USD', 0.053000, '2025-01-15'),
    ('JPY', 'USD', 0.006400, '2025-01-15')
) AS src (FromCurrency, ToCurrency, Rate, AsOf)
    ON  target.FromCurrency = src.FromCurrency
    AND target.ToCurrency   = src.ToCurrency
    AND target.AsOf         = src.AsOf
WHEN NOT MATCHED BY TARGET THEN
    INSERT (FromCurrency, ToCurrency, Rate, AsOf)
    VALUES (src.FromCurrency, src.ToCurrency, src.Rate, src.AsOf);

-- A few sample trades so the report returns data immediately (idempotent on ExternalId).
-- NotionalBase is pre-computed exactly as the ingestion pipeline would compute it.
INSERT INTO dbo.Trades
    (ExternalId, Account, Symbol, Side, Quantity, Price, Currency, TradeTime,
     Notional, NotionalBase, BaseCurrency, RateUsed, RateAsOf)
SELECT v.ExternalId, v.Account, v.Symbol, v.Side, v.Quantity, v.Price, v.Currency, v.TradeTime,
       v.Notional, v.NotionalBase, v.BaseCurrency, v.RateUsed, v.RateAsOf
FROM (VALUES
    ('SEED-001', 'ACC-123', 'MSFT', 'BUY', 100, 310.25, 'USD', '2025-01-15T10:30:00',
        31025.00,  31025.00, 'USD', 1.000000, '2025-01-01'),
    ('SEED-002', 'ACC-123', 'MSFT', 'BUY', 400, 307.75, 'USD', '2025-01-15T14:05:00',
        123100.00, 123100.00, 'USD', 1.000000, '2025-01-01'),
    ('SEED-003', 'ACC-999', 'SAP',  'BUY',  50, 120.00, 'EUR', '2025-01-15T09:00:00',
        6000.00,   6540.00, 'USD', 1.090000, '2025-01-15')
) AS v (ExternalId, Account, Symbol, Side, Quantity, Price, Currency, TradeTime,
        Notional, NotionalBase, BaseCurrency, RateUsed, RateAsOf)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Trades t WHERE t.ExternalId = v.ExternalId);
