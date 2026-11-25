import { ApiError, invalidResponse, isGuid, requestJson } from './http.ts';
import { exactDecimal, JsonNumber, parseExactJson } from '../financial/exactJson.ts';

export type CalculationState = 'not_calculated' | 'current' | 'stale' | 'incomplete' | 'error';
export interface CalculationIssue { field: string; code: string; message: string }
export interface CalculationResult {
  status: 'complete' | 'incomplete' | 'failed';
  costs: Record<'residential' | 'commercial' | 'parking' | 'land' | 'additional', string | null>;
  revenue: Record<'residential' | 'commercial' | 'parking', string | null>;
  totalCosts: string | null; totalRevenue: string | null; profit: string | null;
  roi: string | null; salesProfitability: string | null;
  missingInputs: string[]; errors: CalculationIssue[]; persistenceIssues: CalculationIssue[];
  canPersistToCurrentSchema: boolean;
}
export interface CalculationResponse { state: CalculationState; isCurrent: boolean; result: CalculationResult | null }
const object = (v: unknown): Record<string, unknown> => {
  if (!v || typeof v !== 'object' || Array.isArray(v)) throw invalidResponse();
  return v as Record<string, unknown>;
};
const decimal = (v: unknown, signed = false): string | null => {
  if (v === null) return null;
  if (!(v instanceof JsonNumber)) throw invalidResponse();
  // Partial/error results may contain decimal values wider than stored numeric columns.
  return exactDecimal(v.raw, 57, 28, !signed);
};
function decode(raw: string): CalculationResponse {
  const value = object(parseExactJson(raw));
  if (!['not_calculated','current','stale','incomplete','error'].includes(String(value.state)) || typeof value.isCurrent !== 'boolean') throw invalidResponse();
  const state = value.state as CalculationState;
  if (value.isCurrent !== (state === 'current')) throw invalidResponse();
  if (state === 'not_calculated' || state === 'stale') {
    if (value.result !== null) throw invalidResponse();
    return { state, isCurrent: false, result: null };
  }
  const input = object(value.result), costs = object(input.costs), revenue = object(input.revenue);
  if (!['complete','incomplete','failed'].includes(String(input.status)) || typeof input.canPersistToCurrentSchema !== 'boolean'
    || !Array.isArray(input.missingInputs) || !input.missingInputs.every(v => typeof v === 'string')) throw invalidResponse();
  const issues = (v: unknown): CalculationIssue[] => {
    if (!Array.isArray(v)) throw invalidResponse();
    return v.map(item => { const issue = object(item); if (!['field','code','message'].every(key => typeof issue[key] === 'string')) throw invalidResponse(); return issue as unknown as CalculationIssue; });
  };
  const result: CalculationResult = {
    status: input.status as CalculationResult['status'],
    costs: { residential: decimal(costs.residential), commercial: decimal(costs.commercial), parking: decimal(costs.parking), land: decimal(costs.land), additional: decimal(costs.additional) },
    revenue: { residential: decimal(revenue.residential), commercial: decimal(revenue.commercial), parking: decimal(revenue.parking) },
    totalCosts: decimal(input.totalCosts), totalRevenue: decimal(input.totalRevenue), profit: decimal(input.profit,true),
    roi: decimal(input.roi,true), salesProfitability: decimal(input.salesProfitability,true),
    missingInputs: input.missingInputs as string[], errors: issues(input.errors), persistenceIssues: issues(input.persistenceIssues),
    canPersistToCurrentSchema: input.canPersistToCurrentSchema
  };
  if (state === 'current' && (result.status !== 'complete' || !result.canPersistToCurrentSchema || result.errors.length || result.persistenceIssues.length || result.missingInputs.length
    || [result.totalCosts,result.totalRevenue,result.profit,...Object.values(result.costs),...Object.values(result.revenue)].some(v => v === null))) throw invalidResponse();
  if (state === 'incomplete' && result.status !== 'incomplete') throw invalidResponse();
  return { state, isCurrent: value.isCurrent, result };
}
function checkId(id: string) { if (!isGuid(id) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(id)) throw new ApiError('invalid-request','Выберите финансовую модель.'); }
const decoded = (v: unknown): v is CalculationResponse => Boolean(v && typeof v === 'object' && 'state' in v);
export async function getCalculation(id: string, signal?: AbortSignal): Promise<CalculationResponse> {
  checkId(id); return requestJson(`/financial-models/${id}/calculation`,decoded,signal,{decode});
}
export async function calculateModel(id: string, signal?: AbortSignal): Promise<CalculationResponse> {
  checkId(id); return requestJson(`/financial-models/${id}/calculate`,decoded,signal,{method:'POST',decode,acceptedStatuses:[200,422]});
}
