import { useCalculation } from '../hooks/useCalculation.ts';
import { formatResultMoney as money, formatResultPercent as percent } from '../financial/formatResult.ts';

interface Props { modelId: string | null; projectName: string | null; conceptName: string | null; dirty: boolean; onEdit:()=>void }
const button='rounded-lg bg-sky-700 px-4 py-2 text-sm text-white hover:bg-sky-800 disabled:opacity-50 disabled:cursor-not-allowed';
const labels: Record<string,string>={constructionResidentialArea:'Строительная жилая площадь',constructionCommercialArea:'Строительная коммерческая площадь',residentialArea:'Реализуемая жилая площадь',commercialArea:'Реализуемая коммерческая площадь',parkingSpaces:'Количество машино-мест'};
export function FinancialResultsScreen({modelId,projectName,conceptName,dirty,onEdit}: Props) {
  const calculation=useCalculation(dirty?null:modelId);
  const response=calculation.data;
  const state=response?.state;
  // Error responses may contain invalid numeric components. They are never presented as results.
  const result=(state==='current'||state==='incomplete')?response?.result:null;
  const busy=calculation.loading||calculation.calculating;
  const titles={not_calculated:'Расчёт ещё не выполнен',current:'Расчёт актуален',stale:'Финансовые параметры изменились. Необходимо повторить расчёт',incomplete:'Недостаточно данных для полного расчёта',error:'Результат нельзя корректно вычислить или сохранить'};
  return <section className="panel mt-8" aria-label="Результаты финансовой модели">
    <div className="flex flex-wrap items-start justify-between gap-4">
      <div className="min-w-0"><h2 className="font-semibold">{conceptName??'Концепция не выбрана'}</h2><p className="text-sm text-slate-500">{projectName}</p>
        {modelId&&<p className="mt-2 break-all text-sm text-slate-500">Финансовая модель · {modelId}</p>}</div>
      <button className="rounded-lg border border-slate-300 px-4 py-2 text-sm" onClick={onEdit}>К финансовым параметрам</button>
    </div>
    {!modelId?<p className="mt-6">Выберите или сохраните финансовую модель на экране финансовых параметров.</p>
      :dirty?<p className="mt-6" role="alert">Сначала сохраните изменения финансовых параметров.</p>:<>
        <div className="my-5 flex flex-wrap items-center gap-3"><button className={button} disabled={busy} onClick={()=>void calculation.calculate()}>{calculation.calculating?'Расчёт выполняется…':state==='current'||state==='stale'?'Пересчитать':'Рассчитать'}</button>
          <button className="rounded-lg border border-slate-300 px-4 py-2 text-sm disabled:opacity-50" disabled={busy} onClick={()=>void calculation.reload()}>Обновить результат</button></div>
        {busy&&<p role="status">{calculation.calculating?'Выполняется серверный расчёт…':'Загрузка сохранённого расчёта…'}</p>}
        {calculation.error&&<p role="alert" className="rounded-lg bg-rose-50 p-4 text-rose-900">{calculation.error}</p>}
        {state&&<p role={state==='error'?'alert':'status'} className={'my-4 rounded-lg p-4 '+(state==='current'?'bg-emerald-50 text-emerald-900':'bg-amber-50 text-amber-900')}>{titles[state]}</p>}
        {state==='incomplete'&&result&&<div className="my-4"><p>Частичный результат не сохранён. Заполните недостающие данные:</p><ul className="list-disc pl-5">{result.missingInputs.map(field=><li key={field}>{labels[field]??'Необходимый исходный показатель'}</li>)}</ul></div>}
        {state==='error'&&response?.result&&<ul className="my-4 list-disc pl-5">{[...response.result.errors,...response.result.persistenceIssues].map((issue,index)=><li key={index}>{issue.code==='storageScale'?'Для точного сохранения результата требуется большая дробная точность.':issue.code==='storageRange'||issue.code==='decimalOverflow'?'Результат превышает допустимый диапазон. Проверьте площади и цены.':'Проверьте исходные параметры расчёта.'}</li>)}</ul>}
        {result&&<div data-calculation-state={state}>
          {state==='incomplete'&&<p className="mb-3 text-sm text-amber-900">Доступны только достоверные части неполного расчёта.</p>}
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">{[
            ['Общие затраты',money(result.totalCosts),'totalCosts'],['Потенциальная выручка',money(result.totalRevenue),'totalRevenue'],['Прибыль проекта',money(result.profit),'profit'],
            ['ROI',result.profit===null?'Нет данных':percent(result.roi),'roi'],['Рентабельность продаж',result.profit===null?'Нет данных':percent(result.salesProfitability),'salesProfitability']
          ].map(([title,value,key])=><div key={key} className="min-w-0 rounded-xl bg-slate-50 p-4"><p className="text-sm text-slate-500">{title}</p><p data-result={key} className="mt-2 break-words text-xl font-bold">{value}</p></div>)}</div>
          <div className="mt-7 grid grid-cols-1 gap-7 lg:grid-cols-2">{[
            {title:'Детализация затрат',rows:[['Строительство жилой недвижимости',result.costs.residential],['Строительство коммерческой недвижимости',result.costs.commercial],['Строительство парковок',result.costs.parking],['Земельный участок',result.costs.land],['Дополнительные расходы',result.costs.additional],['Общие затраты',result.totalCosts]]},
            {title:'Детализация выручки',rows:[['Продажа жилой недвижимости',result.revenue.residential],['Продажа коммерческой недвижимости',result.revenue.commercial],['Продажа машино-мест',result.revenue.parking],['Общая выручка',result.totalRevenue]]}
          ].map(group=><div className="min-w-0" key={group.title}><h3 className="mb-3 font-semibold">{group.title}</h3><dl className="divide-y divide-slate-100">{group.rows.map(([title,value])=><div className="flex flex-wrap justify-between gap-2 py-3 text-sm" key={title}><dt className="min-w-0 flex-1">{title}</dt><dd className="break-words font-medium">{money(value)}</dd></div>)}</dl></div>)}</div>
        </div>}
      </>}
  </section>;
}
