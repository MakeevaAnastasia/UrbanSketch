import { formatTep } from '../formatTep.ts';
import type { ConceptVariantDetailResponse } from '../api/types.ts';
import type { useConceptSelection } from '../hooks/useConceptSelection.ts';

type Props = { selection: ReturnType<typeof useConceptSelection> };
const fields: { key: keyof Pick<ConceptVariantDetailResponse,
  'residentialArea' | 'commercialArea' | 'constructionResidentialArea' | 'constructionCommercialArea' |
  'parkingSpaces' | 'residentsCount' | 'buildingsCount' | 'maxFloors' | 'socialAreas' | 'preschoolPlaces' | 'schoolPlaces'>;
  label: string; unit: string }[] = [
  { key: 'residentialArea', label: 'Реализуемая жилая площадь', unit: 'м²' },
  { key: 'commercialArea', label: 'Реализуемая коммерческая площадь', unit: 'м²' },
  { key: 'parkingSpaces', label: 'Машино-места', unit: 'шт.' },
  { key: 'constructionResidentialArea', label: 'Строительная жилая площадь', unit: 'м²' },
  { key: 'constructionCommercialArea', label: 'Строительная коммерческая площадь', unit: 'м²' },
  { key: 'residentsCount', label: 'Количество жителей', unit: 'чел.' },
  { key: 'buildingsCount', label: 'Количество зданий', unit: 'шт.' },
  { key: 'maxFloors', label: 'Максимальная этажность', unit: 'эт.' },
  { key: 'socialAreas', label: 'Площадь социальных площадок', unit: 'м²' },
  { key: 'preschoolPlaces', label: 'Места в ДОУ', unit: 'мест' },
  { key: 'schoolPlaces', label: 'Места в СОУ', unit: 'мест' },
];
function ErrorMessage({ message, retry, label }: { message: string; retry: () => void; label: string }) {
  return <div role="alert" className="my-4 rounded-lg border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">
    <p>{message}</p><button type="button" className="mt-3 rounded-lg bg-sky-700 px-4 py-2 text-white hover:bg-sky-800 focus-visible:outline-2 focus-visible:outline-offset-2" onClick={retry}>{label}</button>
  </div>;
}
export function SourceDataScreen({ selection: s }: Props) {
  const projects = s.projects.data ?? [];
  const variants = s.variants.data ?? [];
  const detail = s.details.status === 'success' ? s.details.data : null;
  return <section className="panel mt-8">
    <h2 className="font-semibold mb-5">Выбор проекта и концепции</h2>
    <div className="mb-6 grid grid-cols-1 gap-4 sm:grid-cols-2">
      <div><label htmlFor="project-select" className="mb-2 block text-sm text-slate-600">Проект</label>
        <select id="project-select" className="w-full rounded-lg border border-slate-300 bg-white p-3 text-sm focus:border-sky-600 focus:outline-sky-600 disabled:bg-slate-100"
          value={s.projectId ?? ''} disabled={s.projects.status !== 'success' || projects.length === 0} onChange={event => s.selectProject(event.target.value || null)}>
          <option value="">Выберите проект</option>{projects.map(project => <option key={project.id} value={project.id}>{project.name}</option>)}
        </select></div>
      <div><label htmlFor="concept-select" className="mb-2 block text-sm text-slate-600">Вариант концепции</label>
        <select id="concept-select" className="w-full rounded-lg border border-slate-300 bg-white p-3 text-sm focus:border-sky-600 focus:outline-sky-600 disabled:bg-slate-100"
          value={s.conceptVariantId ?? ''} disabled={!s.projectId || s.variants.status !== 'success' || variants.length === 0} onChange={event => s.selectConcept(event.target.value || null)}>
          <option value="">Выберите концепцию</option>{variants.map(variant => <option key={variant.id} value={variant.id}>{variant.name}</option>)}
        </select></div>
    </div>
    {s.projects.status === 'loading' && <p role="status" className="text-sm text-slate-500">Загрузка проектов…</p>}
    {s.projects.status === 'error' && <ErrorMessage message={s.projects.error} retry={s.retryProjects} label="Повторить загрузку проектов" />}
    {s.projects.status === 'success' && projects.length === 0 && <p role="status" className="text-sm text-slate-500">В базе пока нет проектов.</p>}
    {s.projects.status === 'success' && projects.length > 0 && !s.projectId && <p className="text-sm text-slate-500">Выберите проект, чтобы увидеть его концепции.</p>}
    {s.projectId && s.variants.status === 'loading' && <p role="status" className="text-sm text-slate-500">Загрузка концепций…</p>}
    {s.variants.status === 'error' && <ErrorMessage message={s.variants.error} retry={s.retryVariants} label="Повторить загрузку концепций" />}
    {s.projectId && s.variants.status === 'success' && variants.length === 0 && <p role="status" className="text-sm text-slate-500">У выбранного проекта пока нет концепций.</p>}
    {s.projectId && variants.length > 0 && !s.conceptVariantId && <p className="text-sm text-slate-500">Выберите концепцию, чтобы увидеть её показатели.</p>}
    {s.details.status === 'loading' && <p role="status" className="text-sm text-slate-500">Загрузка ТЭП…</p>}
    {s.details.status === 'error' && <ErrorMessage message={s.details.error} retry={s.retryDetails} label="Повторить загрузку ТЭП" />}
    {detail && <>
      <h2 className="font-semibold mb-5">{detail.name} · показатели</h2>
      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">{fields.map(field =>
        <div key={field.key} className="rounded-xl border border-slate-200 p-4" data-tep={field.key}>
          <p className="text-slate-500 text-sm">{field.label}</p><p className="text-2xl font-bold mt-1">{formatTep(detail[field.key], field.unit)}</p>
        </div>)}</div>
    </>}
    <p className="hint">Строительные и реализуемые площади — разные показатели. «Нет данных» означает, что показатель не указан.</p>
  </section>;
}
