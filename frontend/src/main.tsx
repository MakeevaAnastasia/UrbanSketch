import React, { useState } from 'react';
import { createRoot } from 'react-dom/client';
import './styles.css';
import { SourceDataScreen } from './components/SourceDataScreen.tsx';
import { useConceptSelection } from './hooks/useConceptSelection.ts';
import { useFinancialModels } from './hooks/useFinancialModels.ts';
import { FinancialParametersScreen } from './components/FinancialParametersScreen.tsx';

type Screen = 'inputs' | 'params' | 'results' | 'compare';
const screens: {id:Screen,title:string,subtitle:string}[] = [
 {id:'inputs',title:'Исходные данные',subtitle:'ТЭП концептуального проекта'},
 {id:'params',title:'Финансовые параметры',subtitle:'Строительство, продажи и затраты'},
 {id:'results',title:'Результаты',subtitle:'Финансовые показатели и денежные потоки'},
 {id:'compare',title:'Сравнение концепций',subtitle:'Сопоставление вариантов застройки'},
];
function App(){
 const [active,setActive] = useState<Screen>('inputs');
 const selection = useConceptSelection();
 const financial = useFinancialModels(selection.conceptVariantId);
 const navigate = (screen: Screen) => { if (screen !== active && financial.canDiscard()) setActive(screen); };
 const guardedSelection = { ...selection,
  selectProject: (id: string | null) => { if (id !== selection.projectId && financial.canDiscard()) selection.selectProject(id); },
  selectConcept: (id: string | null) => { if (id !== selection.conceptVariantId && financial.canDiscard()) selection.selectConcept(id); },
 };
 const item=screens.find(s=>s.id===active)!;
 return <div className="min-h-screen bg-slate-50 text-slate-900 lg:flex">
  <aside className="bg-slate-950 text-white lg:min-h-screen lg:w-64 p-6">
   <div className="text-xl font-bold tracking-tight">UrbanSketch<span className="text-sky-400">.pro</span></div>
   <div className="text-sm text-slate-400 mt-1 mb-8">Финансовый модуль · спринт 2</div>
   <nav aria-label="Разделы финансовой модели" className="grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-1">
     {screens.map((s,i)=><button key={s.id} aria-current={active===s.id?'page':undefined} onClick={()=>navigate(s.id)} className={`px-3 py-3 text-left rounded-lg text-sm ${active===s.id?'bg-sky-600':'bg-slate-800 hover:bg-slate-700'}`}>{i+1}. {s.title}</button>)}
   </nav>
  </aside>
  <main className="mx-auto w-full max-w-6xl p-6 lg:p-10">
   <div className="flex flex-wrap items-center justify-between gap-3"><div><p className="text-sky-700 font-semibold text-xs tracking-widest uppercase">Проект · {selection.selectedProject?.name ?? 'Не выбран'}</p><h1 className="text-3xl font-bold mt-2">{item.title}</h1><p className="text-slate-500 mt-2">{item.subtitle}</p></div><span className="text-xs rounded-full bg-amber-100 text-amber-900 px-3 py-2">{active==='inputs'?'Данные проекта':active==='params'?'Финансовые модели':'Макет · без реальных расчётов'}</span></div>
   {active==='inputs'&&<SourceDataScreen selection={guardedSelection}/>}
   {active==='params'&&<FinancialParametersScreen financial={financial} projectName={selection.selectedProject?.name ?? null} conceptName={selection.selectedConcept?.name ?? null} onChooseConcept={()=>navigate('inputs')}/>}
   {active==='results'&&<section className="panel mt-8"><h2 className="font-semibold mb-5">Эталонный пример из ТЗ</h2><div className="grid grid-cols-2 lg:grid-cols-4 gap-4">{[['Затраты','150 млн ₽'],['Выручка','250 млн ₽'],['Прибыль','100 млн ₽'],['ROI','66,67%']].map(([k,v])=><div key={k} className="rounded-xl bg-slate-50 p-4"><p className="text-sm text-slate-500">{k}</p><p className="font-bold text-xl mt-2">{v}</p></div>)}</div><div className="mt-8"><p className="font-semibold">Годовые денежные потоки (пример)</p><div className="mt-4 space-y-3">{[['Год 1','−90 млн ₽','w-[45%] bg-rose-400'],['Год 2','−10 млн ₽','w-[5%] bg-rose-400'],['Год 3','+200 млн ₽','w-full bg-emerald-500']].map(([year,v,c])=><div key={year} className="grid grid-cols-[70px_1fr_100px] gap-3 items-center text-sm"><span>{year}</span><div className="h-5 bg-slate-100 rounded overflow-hidden"><div className={`h-full ${c}`}/></div><span className="text-right">{v}</span></div>)}</div></div><p className="hint">Статические значения из ТЗ; реальный расчёт — спринты 3–4.</p></section>}
   {active==='compare'&&<section className="panel mt-8"><h2 className="font-semibold mb-5">Сравнение концепций (макет)</h2><div className="overflow-auto"><table className="min-w-full text-sm text-left"><thead className="bg-slate-100"><tr><th className="p-4">Показатель</th><th className="p-4">Концепция А</th><th className="p-4">Концепция Б</th></tr></thead><tbody>{[['Жилая площадь','25 000 м²','22 000 м²'],['Коммерческая площадь','2 000 м²','5 000 м²'],['Затраты','По расчёту','По расчёту'],['Прибыль','По расчёту','По расчёту'],['ROI','По расчёту','По расчёту']].map(([k,a,b])=><tr key={k} className="border-b"><td className="p-4 font-medium">{k}</td><td className="p-4">{a}</td><td className="p-4">{b}</td></tr>)}</tbody></table></div><p className="hint">Сравнение минимум двух концепций и подсветка лучшей — спринт 4.</p></section>}
  </main>
 </div>;
}
createRoot(document.getElementById('root')!).render(<React.StrictMode><App/></React.StrictMode>);
