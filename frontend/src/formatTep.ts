const numbers = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 });
export function formatTep(value: number | null, unit: string): string {
  return value === null ? 'Нет данных' : `${numbers.format(value)} ${unit}`;
}
