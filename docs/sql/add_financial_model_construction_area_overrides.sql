START TRANSACTION;
ALTER TABLE financial_models ADD construction_commercial_area_override numeric(18,2);

ALTER TABLE financial_models ADD construction_residential_area_override numeric(18,2);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261124065820_AddFinancialModelConstructionAreaOverrides', '10.0.15');

COMMIT;
