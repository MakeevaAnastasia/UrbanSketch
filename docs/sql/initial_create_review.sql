CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE projects (
    id uuid NOT NULL,
    name text NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_projects" PRIMARY KEY (id)
);

CREATE TABLE concept_variants (
    id uuid NOT NULL,
    project_id uuid NOT NULL,
    name text NOT NULL,
    residential_sale_area_m2 numeric(18,2) NOT NULL,
    commercial_sale_area_m2 numeric(18,2) NOT NULL,
    residential_construction_area_m2 numeric(18,2),
    commercial_construction_area_m2 numeric(18,2),
    parking_spaces integer NOT NULL,
    population integer,
    buildings_count integer,
    max_storeys integer,
    social_area_m2 numeric(18,2),
    kindergarten_places integer,
    school_places integer,
    CONSTRAINT "PK_concept_variants" PRIMARY KEY (id),
    CONSTRAINT "FK_concept_variants_projects_project_id" FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE CASCADE
);

CREATE TABLE financial_models (
    id uuid NOT NULL,
    variant_id uuid NOT NULL,
    residential_construction_cost_per_m2 numeric(20,2) NOT NULL,
    commercial_construction_cost_per_m2 numeric(20,2) NOT NULL,
    parking_construction_cost_each numeric(20,2) NOT NULL,
    residential_sale_price_per_m2 numeric(20,2) NOT NULL,
    commercial_sale_price_per_m2 numeric(20,2) NOT NULL,
    parking_sale_price_each numeric(20,2) NOT NULL,
    land_cost numeric(20,2) NOT NULL,
    additional_costs numeric(20,2) NOT NULL,
    duration_years integer NOT NULL,
    total_costs numeric(20,2),
    total_revenue numeric(20,2),
    profit numeric(20,2),
    roi_percent numeric(12,4),
    sales_margin_percent numeric(12,4),
    CONSTRAINT "PK_financial_models" PRIMARY KEY (id),
    CONSTRAINT "FK_financial_models_concept_variants_variant_id" FOREIGN KEY (variant_id) REFERENCES concept_variants (id) ON DELETE CASCADE
);

CREATE TABLE cash_flows (
    id uuid NOT NULL,
    financial_model_id uuid NOT NULL,
    period_number integer NOT NULL,
    expense_percent numeric(7,4) NOT NULL,
    revenue_percent numeric(7,4) NOT NULL,
    costs numeric(20,2),
    revenue numeric(20,2),
    net_flow numeric(20,2),
    cumulative_flow numeric(20,2),
    CONSTRAINT "PK_cash_flows" PRIMARY KEY (id),
    CONSTRAINT ck_cash_flows_cost_share CHECK (expense_percent BETWEEN 0 AND 100),
    CONSTRAINT ck_cash_flows_period_number CHECK (period_number > 0),
    CONSTRAINT ck_cash_flows_revenue_share CHECK (revenue_percent BETWEEN 0 AND 100),
    CONSTRAINT "FK_cash_flows_financial_models_financial_model_id" FOREIGN KEY (financial_model_id) REFERENCES financial_models (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX ux_cash_flows_financial_model_id_period_number ON cash_flows (financial_model_id, period_number);

CREATE INDEX "IX_concept_variants_project_id" ON concept_variants (project_id);

CREATE INDEX "IX_financial_models_variant_id" ON financial_models (variant_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111075406_InitialCreate', '10.0.15');

COMMIT;
