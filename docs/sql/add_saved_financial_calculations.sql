START TRANSACTION;
ALTER TABLE financial_models ADD calculation_input_hash character varying(64);

ALTER TABLE financial_models ADD calculation_snapshot text;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261124075506_AddSavedFinancialCalculations', '10.0.15');

COMMIT;
