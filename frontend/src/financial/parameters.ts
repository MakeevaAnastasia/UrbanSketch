import type { FinancialField, FinancialModelResponse, MoneyField, UpdateFinancialModelRequest } from '../api/financialTypes.ts';
import { exactDecimal } from './exactJson.ts';
export type FinancialDraft = Record<FinancialField, string>;
export type FieldErrors = Partial<Record<FinancialField, string>>;
export const moneyFields: MoneyField[] = ['residentialConstructionCost','commercialConstructionCost','parkingCost','residentialSalePrice','commercialSalePrice','parkingSalePrice','landCost','additionalCosts'];
export const financialFields: FinancialField[] = [...moneyFields, 'projectDuration'];
export function emptyDraft(): FinancialDraft { return Object.fromEntries(financialFields.map(key => [key, ''])) as FinancialDraft; }
export function normalizeMoney(input: string): string {
  const text = input.trim();
  if (!text) throw new Error('Обязательное поле. Введите сумму, включая 0 при необходимости.');
  if (!/^(?:\d+|\d{1,3}(?:[ \u00a0\u202f]\d{3})+)(?:[.,]\d{1,2})?$/.test(text))
    throw new Error('Введите неотрицательную сумму: цифры и не более двух знаков после запятой или точки.');
  const normalized = text.replace(/[ \u00a0\u202f]/g, '').replace(',', '.').replace(/^0+(?=\d)/, '');
  try { return exactDecimal(normalized); }
  catch { throw new Error('Сумма превышает диапазон numeric(20,2): максимум 999999999999999999,99.'); }
}
function duration(input: string): number {
  const text = input.trim();
  if (!/^\d+$/.test(text) || BigInt(text) < 1n || BigInt(text) > 2147483647n)
    throw new Error('Введите положительное целое число лет в диапазоне int.');
  return Number(text); // Only int32 duration, never money, is converted to Number.
}
export function validateDraft(draft: FinancialDraft): { parameters: UpdateFinancialModelRequest | null; errors: FieldErrors } {
  const errors: FieldErrors = {};
  const money: Partial<Record<MoneyField, string>> = {};
  for (const key of moneyFields) try { money[key] = normalizeMoney(draft[key]); } catch (error) { errors[key] = (error as Error).message; }
  let years = 0;
  try { years = duration(draft.projectDuration); } catch (error) { errors.projectDuration = (error as Error).message; }
  return { parameters: Object.keys(errors).length ? null : { ...money as Record<MoneyField, string>, projectDuration: years }, errors };
}
export function draftFromModel(model: FinancialModelResponse): FinancialDraft {
  return { ...Object.fromEntries(moneyFields.map(key => [key, model[key].replace('.', ',')])) as Record<MoneyField, string>, projectDuration: String(model.projectDuration) };
}
export function isDraftDirty(draft: FinancialDraft, baseline: FinancialDraft): boolean {
  const fingerprint = (field: FinancialField, value: string) => {
    try { return field === 'projectDuration' ? String(duration(value)) : normalizeMoney(value); }
    catch { return 'invalid:' + value; }
  };
  return financialFields.some(key => fingerprint(key, draft[key]) !== fingerprint(key, baseline[key]));
}
