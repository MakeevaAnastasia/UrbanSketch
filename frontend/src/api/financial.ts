import { ApiError, invalidResponse, isGuid, requestDelete, requestJson } from './http.ts';
import type { CreateFinancialModelRequest, FinancialModelResponse, UpdateFinancialModelRequest } from './financialTypes.ts';
import { exactDecimal, JsonNumber, parseExactJson } from '../financial/exactJson.ts';
import { moneyFields, normalizeMoney } from '../financial/parameters.ts';

function requireId(id: string) { if (!isGuid(id) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(id)) throw new ApiError('invalid-request', 'Выберите корректную концепцию или модель.'); }
function decodeModel(value: unknown): FinancialModelResponse {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) throw invalidResponse();
  const input = value as Record<string, unknown>;
  if (!isGuid(input.id) || !isGuid(input.conceptVariantId) || !(input.projectDuration instanceof JsonNumber)
    || !/^\d+$/.test(input.projectDuration.raw)) throw invalidResponse();
  const years = BigInt(input.projectDuration.raw);
  if (years < 1n || years > 2147483647n) throw invalidResponse();
  const output: Record<string, unknown> = { id: input.id, conceptVariantId: input.conceptVariantId, projectDuration: Number(years) };
  for (const key of [...moneyFields, 'totalCosts', 'totalRevenue', 'profit', 'roi', 'salesProfitability']) {
    const isInput = moneyFields.includes(key as typeof moneyFields[number]);
    if (!isInput && input[key] === null) output[key] = null;
    else {
      if (!(input[key] instanceof JsonNumber)) throw invalidResponse();
      output[key] = key === 'roi' || key === 'salesProfitability'
        ? exactDecimal(input[key].raw, 12, 4, false) : exactDecimal(input[key].raw, 20, 2, isInput);
    }
  }
  return output as FinancialModelResponse;
}
const decodeOne = (raw: string): FinancialModelResponse => decodeModel(parseExactJson(raw));
const decodedModel = (value: unknown): value is FinancialModelResponse => typeof value === 'object' && value !== null && 'id' in value;
function decodeList(raw: string): FinancialModelResponse[] {
  const value = parseExactJson(raw);
  if (!Array.isArray(value)) throw invalidResponse();
  const models = value.map(decodeModel);
  if (new Set(models.map(model => model.id.toLowerCase())).size !== models.length) throw invalidResponse();
  return models;
}
export function serializeParameters(data: UpdateFinancialModelRequest, conceptId?: string): string {
  if (conceptId !== undefined) requireId(conceptId);
  if (!Number.isInteger(data.projectDuration) || data.projectDuration < 1 || data.projectDuration > 2147483647) throw new ApiError('invalid-request', 'Неверный срок проекта.');
  const pairs = moneyFields.map(key => `${JSON.stringify(key)}:${normalizeMoney(data[key])}`);
  pairs.push(`"projectDuration":${data.projectDuration}`);
  if (conceptId !== undefined) pairs.push(`"conceptVariantId":${JSON.stringify(conceptId)}`);
  return '{' + pairs.join(',') + '}';
}
export async function getFinancialModels(id: string, signal?: AbortSignal): Promise<FinancialModelResponse[]> {
  requireId(id);
  const models = await requestJson(`/concepts/${id}/financial-models`, (v): v is FinancialModelResponse[] => Array.isArray(v), signal, { decode: decodeList });
  if (models.some(model => model.conceptVariantId.toLowerCase() !== id.toLowerCase())) throw invalidResponse();
  return models;
}
export async function getFinancialModel(id: string, signal?: AbortSignal): Promise<FinancialModelResponse> {
  requireId(id);
  const model = await requestJson(`/financial-models/${id}`, decodedModel, signal, { decode: decodeOne });
  if (model.id.toLowerCase() !== id.toLowerCase()) throw invalidResponse();
  return model;
}
export async function createFinancialModel(data: CreateFinancialModelRequest): Promise<FinancialModelResponse> {
  const model = await requestJson('/financial-models', decodedModel, undefined, { method: 'POST', status: 201,
    body: serializeParameters(data, data.conceptVariantId), decode: decodeOne });
  if (model.conceptVariantId.toLowerCase() !== data.conceptVariantId.toLowerCase()) throw invalidResponse();
  return model;
}
export async function updateFinancialModel(id: string, data: UpdateFinancialModelRequest): Promise<FinancialModelResponse> {
  requireId(id);
  const model = await requestJson(`/financial-models/${id}`, decodedModel, undefined, { method: 'PUT', body: serializeParameters(data), decode: decodeOne });
  if (model.id.toLowerCase() !== id.toLowerCase()) throw invalidResponse();
  return model;
}
export async function deleteFinancialModel(id: string): Promise<void> { requireId(id); await requestDelete(`/financial-models/${id}`); }
