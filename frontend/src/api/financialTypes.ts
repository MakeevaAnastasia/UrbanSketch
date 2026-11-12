// Wire DTO decimals are JSON numbers; the client keeps exact decimal text instead of Number.
export type MoneyField = 'residentialConstructionCost' | 'commercialConstructionCost' | 'parkingCost'
  | 'residentialSalePrice' | 'commercialSalePrice' | 'parkingSalePrice' | 'landCost' | 'additionalCosts';
export type FinancialField = MoneyField | 'projectDuration';
export type UpdateFinancialModelRequest = Record<MoneyField, string> & { projectDuration: number };
export type CreateFinancialModelRequest = UpdateFinancialModelRequest & { conceptVariantId: string };
export type FinancialModelResponse = UpdateFinancialModelRequest & {
  id: string;
  conceptVariantId: string;
  totalCosts: string | null;
  totalRevenue: string | null;
  profit: string | null;
  roi: string | null;
  salesProfitability: string | null;
};
