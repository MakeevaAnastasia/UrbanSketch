import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError, isAbortError } from '../api/http.ts';
import { createFinancialModel, deleteFinancialModel, getFinancialModel, getFinancialModels, updateFinancialModel } from '../api/financial.ts';
import type { FinancialField, FinancialModelResponse } from '../api/financialTypes.ts';
import { draftFromModel, emptyDraft, financialFields, isDraftDirty, validateDraft } from '../financial/parameters.ts';
import type { FieldErrors, FinancialDraft } from '../financial/parameters.ts';

interface State {
  conceptId: string | null; models: FinancialModelResponse[]; listStatus: 'idle' | 'loading' | 'success' | 'error';
  listError: string; mode: 'none' | 'new' | 'edit'; selectedId: string | null; detailLoading: boolean; detailReady: boolean;
  draft: FinancialDraft; baseline: FinancialDraft; errors: FieldErrors; error: string; notice: string;
  busy: 'saving' | 'deleting' | null; uncertainPost: boolean;
}
const initial = (conceptId: string | null): State => ({ conceptId, models: [], listStatus: conceptId ? 'loading' : 'idle',
  listError: '', mode: 'none', selectedId: null, detailLoading: false, detailReady: false, draft: emptyDraft(), baseline: emptyDraft(),
  errors: {}, error: '', notice: '', busy: null, uncertainPost: false });
const message = (error: unknown) => error instanceof ApiError ? error.message : 'Не удалось выполнить действие. Повторите попытку.';

export function useFinancialModels(conceptId: string | null) {
  const [state, setState] = useState<State>(() => initial(conceptId));
  const current = useRef(state);
  const context = useRef<string | null>(conceptId);
  const listController = useRef<AbortController | null>(null);
  const detailController = useRef<AbortController | null>(null);
  const detailVersion = useRef(0);
  const busy = useRef(false);
  const replace = useCallback((next: State) => { current.current = next; setState(next); }, []);
  const patch = useCallback((change: Partial<State>) => { const next = { ...current.current, ...change }; current.current = next; setState(next); }, []);

  const loadModel = useCallback(async (cid: string, id: string) => {
    detailController.current?.abort();
    const controller = new AbortController(); detailController.current = controller;
    const version = ++detailVersion.current;
    patch({ mode: 'edit', selectedId: id, detailLoading: true, detailReady: false, draft: emptyDraft(), baseline: emptyDraft(), errors: {}, error: '', notice: '', uncertainPost: false });
    try {
      const model = await getFinancialModel(id, controller.signal);
      if (context.current !== cid || version !== detailVersion.current || controller.signal.aborted) return;
      if (model.conceptVariantId.toLowerCase() !== cid.toLowerCase()) throw new ApiError('invalid-response', 'Модель не принадлежит выбранной концепции. Обновите список.');
      const draft = draftFromModel(model);
      patch({ draft, baseline: draft, detailLoading: false, detailReady: true });
    } catch (error: unknown) {
      if (context.current === cid && version === detailVersion.current && !isAbortError(error) && !controller.signal.aborted)
        patch({ detailLoading: false, error: message(error) });
    }
  }, [patch]);

  const loadList = useCallback(async (cid: string, chooseFirst = false) => {
    listController.current?.abort(); const controller = new AbortController(); listController.current = controller;
    patch({ listStatus: 'loading', listError: '' });
    try {
      const models = await getFinancialModels(cid, controller.signal);
      if (context.current !== cid || controller.signal.aborted) return;
      patch({ models, listStatus: 'success', listError: '', uncertainPost: false });
      if (chooseFirst && models.length > 0 && current.current.mode === 'none') await loadModel(cid, models[0].id);
    } catch (error: unknown) {
      if (context.current === cid && !controller.signal.aborted && !isAbortError(error)) patch({ listStatus: 'error', listError: message(error) });
    }
  }, [patch, loadModel]);

  useEffect(() => {
    context.current = conceptId; detailVersion.current++;
    detailController.current?.abort(); listController.current?.abort();
    replace(initial(conceptId));
    if (conceptId) void loadList(conceptId, true);
    return () => { detailVersion.current++; detailController.current?.abort(); listController.current?.abort(); };
  }, [conceptId, replace, loadList]);

  const visible = state.conceptId === conceptId ? state : initial(conceptId);
  const dirty = isDraftDirty(visible.draft, visible.baseline);
  useEffect(() => {
    if (!dirty) return;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ''; };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [dirty]);

  const canDiscard = () => {
    if (busy.current) { patch({ error: 'Дождитесь завершения сохранения или удаления.' }); return false; }
    const value = current.current;
    if (isDraftDirty(value.draft, value.baseline)) {
      if (!window.confirm('Есть несохранённые изменения. Выйти без сохранения?')) return false;
      patch({ draft: { ...value.baseline }, errors: {}, error: '', notice: '' });
    }
    return true;
  };
  const newModel = () => {
    if (!conceptId || !canDiscard()) return;
    detailController.current?.abort(); detailVersion.current++;
    patch({ mode: 'new', selectedId: null, detailLoading: false, detailReady: true, draft: emptyDraft(), baseline: emptyDraft(), errors: {}, error: '', notice: '', uncertainPost: false });
  };
  const chooseModel = (id: string) => {
    if (id === current.current.selectedId && !current.current.error) return;
    if (!conceptId || !canDiscard()) return;
    if (id) void loadModel(conceptId, id);
    else { detailController.current?.abort(); detailVersion.current++; patch({ mode: 'none', selectedId: null, detailLoading: false, draft: emptyDraft(), baseline: emptyDraft(), error: '', errors: {}, notice: '' }); }
  };
  const changeField = (field: FinancialField, value: string) => {
    if (busy.current) return;
    patch({ draft: { ...current.current.draft, [field]: value }, errors: { ...current.current.errors, [field]: undefined }, notice: '' });
  };
  const save = async () => {
    const source = current.current;
    if (busy.current || !source.conceptId || !source.detailReady || source.detailLoading || source.mode === 'none' || source.uncertainPost) return;
    const { parameters, errors } = validateDraft(source.draft);
    if (!parameters) { patch({ errors, error: 'Проверьте поля формы.', notice: '' }); return; }
    busy.current = true; patch({ busy: 'saving', error: '', errors: {}, notice: '' });
    try {
      const model = source.mode === 'new' ? await createFinancialModel({ ...parameters, conceptVariantId: source.conceptId })
        : await updateFinancialModel(source.selectedId!, parameters);
      if (context.current !== source.conceptId) return;
      if (model.conceptVariantId.toLowerCase() !== source.conceptId.toLowerCase()) throw new ApiError('invalid-response', 'Ответ модели не соответствует выбранной концепции.');
      const draft = draftFromModel(model);
      patch({ mode: 'edit', selectedId: model.id, draft, baseline: draft,
        models: [...current.current.models.filter(item => item.id !== model.id), model].sort((a, b) => a.id.localeCompare(b.id)),
        notice: 'Финансовая модель сохранена', uncertainPost: false });
      await loadList(source.conceptId);
    } catch (error: unknown) {
      if (context.current !== source.conceptId) return;
      const errors: FieldErrors = {};
      if (error instanceof ApiError)
        for (const field of financialFields) if (error.validationErrors[field]) errors[field] = error.validationErrors[field].join(' ');
      const uncertain = source.mode === 'new' && error instanceof ApiError && (error.kind === 'network' || error.kind === 'invalid-response');
      const general = error instanceof ApiError ? Object.entries(error.validationErrors).filter(([key]) => !financialFields.includes(key as FinancialField)).flatMap(([, values]) => values).join(' ') : '';
      patch({ errors, error: uncertain ? 'Не удалось подтвердить создание. Обновите список моделей перед повторным сохранением.' : [message(error), general].filter(Boolean).join(' '), uncertainPost: uncertain });
    } finally { busy.current = false; if (context.current === source.conceptId) patch({ busy: null }); }
  };
  const remove = async () => {
    const source = current.current;
    if (busy.current || !source.conceptId || source.mode !== 'edit' || !source.selectedId || source.detailLoading) return;
    const warning = isDraftDirty(source.draft, source.baseline) ? 'Несохранённые изменения будут потеряны. ' : '';
    if (!window.confirm(warning + 'Удалить выбранную финансовую модель?')) return;
    busy.current = true; patch({ busy: 'deleting', error: '', notice: '' });
    try {
      await deleteFinancialModel(source.selectedId);
      if (context.current !== source.conceptId) return;
      patch({ models: current.current.models.filter(model => model.id !== source.selectedId), mode: 'none', selectedId: null,
        draft: emptyDraft(), baseline: emptyDraft(), errors: {}, notice: 'Финансовая модель удалена' });
      await loadList(source.conceptId);
    } catch (error: unknown) { if (context.current === source.conceptId) patch({ error: message(error) }); }
    finally { busy.current = false; if (context.current === source.conceptId) patch({ busy: null }); }
  };
  return { ...visible, dirty, canDiscard, newModel, chooseModel, changeField, save, remove,
    reload: () => { if (conceptId && !busy.current) void loadList(conceptId); },
    retryModel: () => { if (conceptId && visible.selectedId && canDiscard()) void loadModel(conceptId, visible.selectedId); } };
}
