import type { FinancialField } from '../api/financialTypes.ts';
import type { useFinancialModels } from '../hooks/useFinancialModels.ts';

type Props = { financial: ReturnType<typeof useFinancialModels>; projectName: string | null; conceptName: string | null; onChooseConcept: () => void };
const groups: { title: string; fields: { key: FinancialField; label: string; unit: string }[] }[] = [
  { title: 'Затраты на строительство', fields: [
    { key: 'residentialConstructionCost', label: 'Стоимость строительства жилья', unit: 'руб./м²' },
    { key: 'commercialConstructionCost', label: 'Стоимость строительства коммерции', unit: 'руб./м²' },
    { key: 'parkingCost', label: 'Себестоимость машино-места', unit: 'руб./шт.' }] },
  { title: 'Цены продажи', fields: [
    { key: 'residentialSalePrice', label: 'Цена продажи жилья', unit: 'руб./м²' },
    { key: 'commercialSalePrice', label: 'Цена продажи коммерции', unit: 'руб./м²' },
    { key: 'parkingSalePrice', label: 'Цена продажи машино-места', unit: 'руб./шт.' }] },
  { title: 'Дополнительные расходы и срок', fields: [
    { key: 'landCost', label: 'Стоимость земельного участка', unit: 'руб.' },
    { key: 'additionalCosts', label: 'Прочие дополнительные расходы', unit: 'руб.' },
    { key: 'projectDuration', label: 'Продолжительность проекта', unit: 'лет' }] },
];
const button = 'rounded-lg bg-sky-700 px-4 py-2 text-sm text-white hover:bg-sky-800 disabled:cursor-not-allowed disabled:opacity-50';
export function FinancialParametersScreen({ financial: f, projectName, conceptName, onChooseConcept }: Props) {
  if (!f.conceptId) return <section className="panel mt-8"><p>Сначала выберите проект и концепцию на экране «Исходные данные».</p>
    <button className={button + ' mt-4'} onClick={onChooseConcept}>К исходным данным</button></section>;
  return <section className="panel mt-8">
    <h2 className="font-semibold">{conceptName ?? 'Выбранная концепция'}</h2><p className="mt-1 text-sm text-slate-500">{projectName}</p>
    <div className="my-5 flex flex-wrap items-end gap-3">
      <div className="min-w-0 flex-1"><label htmlFor="financial-model-select" className="mb-2 block text-sm text-slate-600">Сохранённая финансовая модель</label>
        <select id="financial-model-select" value={f.selectedId ?? ''} disabled={Boolean(f.busy) || f.listStatus === 'loading' || f.models.length === 0}
          onChange={event => f.chooseModel(event.target.value)} className="w-full rounded-lg border border-slate-300 bg-white p-3 text-sm disabled:bg-slate-100">
          <option value="">Выберите модель</option>{f.models.map((model, index) => <option key={model.id} value={model.id}>Модель {index + 1} · {model.id.slice(0, 8)}…{model.id.slice(-4)}</option>)}
        </select></div>
      <button className={button} disabled={Boolean(f.busy) || f.listStatus !== 'success'} onClick={f.newModel}>Создать модель</button>
      <button className="rounded-lg border border-slate-300 px-4 py-2 text-sm disabled:opacity-50" disabled={Boolean(f.busy) || f.listStatus === 'loading'} onClick={f.reload}>Обновить список</button>
    </div>
    {f.listStatus === 'loading' && <p role="status" className="my-3 text-sm text-slate-500">Загрузка списка моделей…</p>}
    {f.listError && <div role="alert" className="my-3 rounded-lg bg-rose-50 p-3 text-sm text-rose-900">{f.listError} <button className="underline" onClick={f.reload} disabled={Boolean(f.busy)}>Повторить загрузку моделей</button></div>}
    {f.listStatus === 'success' && f.models.length === 0 && f.mode === 'none' && <p className="my-4 text-sm text-slate-500">У этой концепции ещё нет финансовых моделей. Нажмите «Создать модель».</p>}
    {f.detailLoading && <p role="status" className="my-4 text-sm text-slate-500">Загрузка финансовой модели…</p>}
    {f.error && <div role="alert" className="my-4 rounded-lg border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">{f.error}
      {f.mode === 'edit' && !f.detailReady && !f.detailLoading && <button className="ml-2 underline" onClick={f.retryModel} disabled={Boolean(f.busy)}>Повторить загрузку модели</button>}</div>}
    {f.notice && <p role="status" className="my-4 rounded-lg bg-emerald-50 p-3 text-sm text-emerald-900">{f.notice}</p>}
    {f.mode !== 'none' && f.detailReady && !f.detailLoading && <form noValidate onSubmit={event => { event.preventDefault(); void f.save(); }}>
      <p className="mb-4 text-sm text-slate-600">{f.mode === 'new' ? 'Новая финансовая модель' : 'Редактирование сохранённой модели'}{f.dirty ? ' · Есть несохранённые изменения' : ''}</p>
      <fieldset disabled={Boolean(f.busy)} className="min-w-0 space-y-6">{groups.map(group => <div key={group.title}>
        <h3 className="mb-4 font-semibold">{group.title}</h3><div className="grid grid-cols-1 gap-4 md:grid-cols-2">{group.fields.map(field =>
          <div key={field.key} className="min-w-0"><label className="mb-2 block text-sm text-slate-600" htmlFor={'finance-' + field.key}>{field.label}, {field.unit}</label>
            <input id={'finance-' + field.key} name={field.key} type="text" inputMode={field.key === 'projectDuration' ? 'numeric' : 'decimal'} autoComplete="off"
              value={f.draft[field.key]} onChange={event => f.changeField(field.key, event.target.value)} aria-invalid={Boolean(f.errors[field.key])}
              aria-describedby={f.errors[field.key] ? 'error-' + field.key : undefined}
              className={'w-full rounded-lg border bg-white p-3 text-sm focus:outline-sky-600 disabled:bg-slate-100 ' + (f.errors[field.key] ? 'border-rose-500' : 'border-slate-300')} />
            {f.errors[field.key] && <p id={'error-' + field.key} className="mt-1 text-sm text-rose-700">{f.errors[field.key]}</p>}
          </div>)}</div></div>)}</fieldset>
      <div className="mt-6 flex flex-wrap gap-3"><button type="submit" className={button} disabled={Boolean(f.busy) || f.uncertainPost}>{f.busy === 'saving' ? 'Сохранение…' : 'Сохранить'}</button>
        {f.mode === 'edit' && <button type="button" onClick={() => void f.remove()} disabled={Boolean(f.busy)} className="rounded-lg border border-rose-300 px-4 py-2 text-sm text-rose-800 disabled:opacity-50">{f.busy === 'deleting' ? 'Удаление…' : 'Удалить модель'}</button>}</div>
      <p className="hint">Введите все параметры. Ноль допускается для денежных значений; пустое поле не равно нулю. Десятичный разделитель — запятая или точка.</p>
    </form>}
  </section>;
}
