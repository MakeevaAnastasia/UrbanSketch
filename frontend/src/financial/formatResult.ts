// Formatting only. No client-side financial indicators are computed here.
function rounded(value: string, places: number): string {
  if (!/^-?\d+(?:\.\d+)?$/.test(value)) throw new Error('Invalid decimal text');
  const negative = value.startsWith('-');
  const [whole, fraction=''] = (negative ? value.slice(1) : value).split('.');
  let units = BigInt(whole + fraction.padEnd(places,'0').slice(0,places));
  if ((fraction[places] ?? '0') >= '5') units++;
  const digits = units.toString().padStart(places+1,'0');
  return (negative && units !== 0n ? '-' : '') + digits.slice(0,-places) + ',' + digits.slice(-places);
}
export function formatResultMoney(value: string | null): string {
  if (value === null) return 'Нет данных';
  const [whole, fraction] = rounded(value,2).split(',');
  return whole.replace(/\B(?=(\d{3})+(?!\d))/g,' ') + (fraction === '00' ? '' : ','+fraction) + ' ₽';
}
export function formatResultPercent(value: string | null): string {
  return value === null ? 'Не рассчитывается' : rounded(value,2)+'%';
}
