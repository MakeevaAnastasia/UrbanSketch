const numbers = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 });
export function formatTep(value: number | string | null, unit: string): string {
  return value === null ? 'Нет данных' : `${typeof value === 'string' ? value.replace(/(\.\d*?[1-9])0+$|\.0+$/, '$1').replace('.', ',') : numbers.format(value)} ${unit}`;
}
