-- ПРОЕКТНАЯ СХЕМА БД ДЛЯ СПРИНТА 1. НЕ ПРИМЕНЕНА К РАБОЧЕЙ БАЗЕ.
-- Реализация миграций и ограничения будет проверена во 2-м спринте.
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE project (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name TEXT NOT NULL CHECK (length(trim(name)) > 0),
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE concept_variant (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  project_id UUID NOT NULL REFERENCES project(id) ON DELETE CASCADE,
  name TEXT NOT NULL CHECK (length(trim(name)) > 0),
  residential_sale_area_m2 NUMERIC(18,2) NOT NULL DEFAULT 0 CHECK (residential_sale_area_m2 >= 0),
  commercial_sale_area_m2 NUMERIC(18,2) NOT NULL DEFAULT 0 CHECK (commercial_sale_area_m2 >= 0),
  population INTEGER CHECK (population >= 0),
  parking_spaces INTEGER NOT NULL DEFAULT 0 CHECK (parking_spaces >= 0),
  buildings_count INTEGER CHECK (buildings_count >= 0),
  max_storeys INTEGER CHECK (max_storeys >= 0),
  social_area_m2 NUMERIC(18,2) CHECK (social_area_m2 >= 0),
  kindergarten_places INTEGER CHECK (kindergarten_places >= 0),
  school_places INTEGER CHECK (school_places >= 0),
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE financial_model (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  variant_id UUID NOT NULL REFERENCES concept_variant(id) ON DELETE CASCADE,
  name TEXT NOT NULL CHECK (length(trim(name)) > 0),
  duration_years INTEGER NOT NULL CHECK (duration_years > 0),
  currency_code CHAR(3) NOT NULL DEFAULT 'RUB', -- предварительно, уточнить
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE financial_input (
  model_id UUID PRIMARY KEY REFERENCES financial_model(id) ON DELETE CASCADE,
  residential_construction_area_m2 NUMERIC(18,2) NOT NULL CHECK (residential_construction_area_m2 >= 0),
  commercial_construction_area_m2 NUMERIC(18,2) NOT NULL CHECK (commercial_construction_area_m2 >= 0),
  parking_spaces_for_construction INTEGER NOT NULL CHECK (parking_spaces_for_construction >= 0),
  parking_spaces_for_sale INTEGER NOT NULL CHECK (parking_spaces_for_sale >= 0),
  residential_construction_cost_per_m2 NUMERIC(18,2) NOT NULL CHECK (residential_construction_cost_per_m2 >= 0),
  commercial_construction_cost_per_m2 NUMERIC(18,2) NOT NULL CHECK (commercial_construction_cost_per_m2 >= 0),
  parking_construction_cost_each NUMERIC(18,2) NOT NULL CHECK (parking_construction_cost_each >= 0),
  residential_sale_price_per_m2 NUMERIC(18,2) NOT NULL CHECK (residential_sale_price_per_m2 >= 0),
  commercial_sale_price_per_m2 NUMERIC(18,2) NOT NULL CHECK (commercial_sale_price_per_m2 >= 0),
  parking_sale_price_each NUMERIC(18,2) NOT NULL CHECK (parking_sale_price_each >= 0),
  land_cost NUMERIC(18,2) NOT NULL DEFAULT 0 CHECK (land_cost >= 0),
  additional_costs NUMERIC(18,2) NOT NULL DEFAULT 0 CHECK (additional_costs >= 0)
);
CREATE TABLE annual_allocation (
  model_id UUID NOT NULL REFERENCES financial_model(id) ON DELETE CASCADE,
  year_number INTEGER NOT NULL CHECK (year_number > 0),
  expense_percent NUMERIC(5,2) NOT NULL CHECK (expense_percent BETWEEN 0 AND 100),
  revenue_percent NUMERIC(5,2) NOT NULL CHECK (revenue_percent BETWEEN 0 AND 100),
  PRIMARY KEY (model_id, year_number)
);
-- CHECK для сумм 100% невозможно реализовать обычным построчным CHECK;
-- правило контролируется в доменном сервисе в спринте 4.
CREATE TABLE financial_result (
  model_id UUID PRIMARY KEY REFERENCES financial_model(id) ON DELETE CASCADE,
  total_costs NUMERIC(18,2) NOT NULL,
  total_revenue NUMERIC(18,2) NOT NULL,
  profit NUMERIC(18,2) NOT NULL,
  roi_percent NUMERIC(12,4),
  sales_margin_percent NUMERIC(12,4),
  calculated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE annual_cash_flow (
  model_id UUID NOT NULL REFERENCES financial_model(id) ON DELETE CASCADE,
  year_number INTEGER NOT NULL CHECK (year_number > 0),
  costs NUMERIC(18,2) NOT NULL,
  revenue NUMERIC(18,2) NOT NULL,
  net_flow NUMERIC(18,2) NOT NULL,
  cumulative_flow NUMERIC(18,2) NOT NULL,
  PRIMARY KEY (model_id, year_number)
);
CREATE INDEX ix_concept_variant_project_id ON concept_variant(project_id);
CREATE INDEX ix_financial_model_variant_id ON financial_model(variant_id);
