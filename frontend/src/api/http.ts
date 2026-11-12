export type ApiErrorKind = 'http' | 'network' | 'invalid-response' | 'invalid-request';
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly status: number | undefined;
  readonly validationErrors: Record<string, string[]>;
  constructor(kind: ApiErrorKind, message: string, status?: number, errors: Record<string, string[]> = {}) {
    super(message); this.name = 'ApiError'; this.kind = kind; this.status = status; this.validationErrors = errors;
  }
}
export const isGuid = (value: unknown): value is string => typeof value === 'string' && /^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i.test(value);
export const isAbortError = (error: unknown): boolean => error instanceof Error && error.name === 'AbortError';
export const invalidResponse = () => new ApiError('invalid-response', 'Сервер вернул некорректные данные. Попробуйте загрузить их ещё раз.');
const baseUrl = (import.meta.env?.VITE_API_BASE_URL?.trim() || '/api').replace(/\/+$/, '');
interface Options { method?: 'GET' | 'POST' | 'PUT' | 'DELETE'; body?: string; status?: number; decode?: (raw: string) => unknown }
async function response(path: string, signal?: AbortSignal, options: Options = {}): Promise<Response> {
  let result: Response;
  try { result = await fetch(`${baseUrl}${path}`, { method: options.method ?? 'GET', signal,
    headers: { Accept: 'application/json', ...(options.body === undefined ? {} : { 'Content-Type': 'application/json' }) }, body: options.body }); }
  catch (error: unknown) {
    if (isAbortError(error) || signal?.aborted) throw error;
    throw new ApiError('network', 'Не удалось связаться с сервером. Проверьте подключение и повторите попытку.');
  }
  if (!result.ok) {
    const errors: Record<string, string[]> = Object.create(null);
    if (result.status === 400) {
      try {
        const problem: unknown = await result.json();
        if (typeof problem === 'object' && problem !== null && 'errors' in problem && typeof problem.errors === 'object' && problem.errors !== null)
          for (const [key, value] of Object.entries(problem.errors))
            if (Array.isArray(value) && value.every(item => typeof item === 'string')) errors[key] = value;
      } catch { /* An invalid problem response must not expose raw server details. */ }
    }
    throw new ApiError('http', result.status === 400 ? 'Проверьте введённые значения.' : result.status === 404
      ? 'Данные не найдены. Попробуйте обновить список.' : 'Сервер не смог загрузить данные. Повторите попытку позже.', result.status, errors);
  }
  if (result.status !== (options.status ?? 200)) throw invalidResponse();
  return result;
}
export async function requestJson<T>(path: string, check: (value: unknown) => value is T, signal?: AbortSignal, options: Options = {}): Promise<T> {
  const result = await response(path, signal, options);
  if (result.headers.get('content-type')?.split(';')[0].trim().toLowerCase() !== 'application/json') throw invalidResponse();
  let value: unknown;
  try { value = (options.decode ?? JSON.parse)(await result.text()); }
  catch (error: unknown) { if (isAbortError(error) || signal?.aborted) throw error; throw invalidResponse(); }
  if (!check(value)) throw invalidResponse();
  return value;
}
export async function requestDelete(path: string, signal?: AbortSignal): Promise<void> {
  await response(path, signal, { method: 'DELETE', status: 204 });
}
