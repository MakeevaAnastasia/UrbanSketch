// Only number tokens become this class. JSON string/object values cannot masquerade as numbers.
export class JsonNumber {
  readonly raw: string;
  constructor(raw: string) { this.raw = raw; }
}
export function parseExactJson(source: string): unknown {
  let position = 0;
  const whitespace = () => { while (/[ \t\r\n]/.test(source[position] ?? '') && position < source.length) position++; };
  const fail = (): never => { throw new SyntaxError('Invalid JSON'); };
  const string = (): string => {
    const start = position++;
    while (position < source.length) {
      const character = source[position++];
      if (character === '\\') position++;
      else if (character === '"') return JSON.parse(source.slice(start, position)) as string;
    }
    return fail();
  };
  const value = (): unknown => {
    whitespace();
    if (source[position] === '"') return string();
    if (source[position] === '{') {
      position++; whitespace();
      const object: Record<string, unknown> = Object.create(null);
      if (source[position] === '}') { position++; return object; }
      while (true) {
        whitespace(); if (source[position] !== '"') return fail();
        const key = string(); whitespace();
        if (Object.hasOwn(object, key) || source[position++] !== ':') return fail();
        object[key] = value(); whitespace();
        const delimiter = source[position++];
        if (delimiter === '}') return object;
        if (delimiter !== ',') return fail();
      }
    }
    if (source[position] === '[') {
      position++; whitespace(); const array: unknown[] = [];
      if (source[position] === ']') { position++; return array; }
      while (true) {
        array.push(value()); whitespace(); const delimiter = source[position++];
        if (delimiter === ']') return array;
        if (delimiter !== ',') return fail();
      }
    }
    for (const [token, result] of [['true', true], ['false', false], ['null', null]] as const)
      if (source.startsWith(token, position)) { position += token.length; return result; }
    const number = source.slice(position).match(/^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?/);
    if (!number) return fail();
    position += number[0].length;
    return new JsonNumber(number[0]);
  };
  const result = value(); whitespace();
  if (position !== source.length) return fail();
  return result;
}

export function exactDecimal(raw: string, precision = 20, places = 2, nonnegative = true): string {
  if (!/^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$/.test(raw)) throw new Error('Invalid decimal');
  const negative = raw.startsWith('-');
  const [mantissa, exponent = '0'] = (negative ? raw.slice(1) : raw).split(/[eE]/);
  let digits = mantissa.replace('.', '').replace(/^0+/, '');
  if (!digits) return '0.' + '0'.repeat(places);
  if (negative && nonnegative) throw new Error('Negative decimal');
  let scale = BigInt(mantissa.includes('.') ? mantissa.length - mantissa.indexOf('.') - 1 : 0) - BigInt(exponent);
  if (scale > 0n) {
    const zeroes = digits.length - digits.replace(/0+$/, '').length;
    const trim = Number(scale < BigInt(zeroes) ? scale : BigInt(zeroes));
    if (trim) { digits = digits.slice(0, -trim); scale -= BigInt(trim); }
  }
  if (scale > BigInt(places) || BigInt(digits.length) - scale > BigInt(precision - places)) throw new Error('Decimal range/scale');
  if (scale < 0n) { digits += '0'.repeat(Number(-scale)); scale = 0n; }
  digits = digits.padStart(Number(scale) + 1, '0');
  const whole = digits.slice(0, digits.length - Number(scale));
  const fraction = scale ? digits.slice(-Number(scale)) : '';
  return (negative ? '-' : '') + whole + '.' + fraction.padEnd(places, '0');
}
