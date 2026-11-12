import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError, getProjects, getProjectVariants, getConceptDetails, isAbortError } from '../api/client.ts';
import type { ProjectResponse, ConceptVariantSummaryResponse, ConceptVariantDetailResponse } from '../api/types.ts';

export type Resource<T> =
  | { key: string | null; status: 'idle' | 'loading'; data: null; error: null }
  | { key: string | null; status: 'success'; data: T; error: null }
  | { key: string | null; status: 'error'; data: null; error: string };
const idle = <T,>(): Resource<T> => ({ key: null, status: 'idle', data: null, error: null });
const loading = <T,>(key: string): Resource<T> => ({ key, status: 'loading', data: null, error: null });
const failure = <T,>(key: string, error: unknown): Resource<T> => ({ key, status: 'error', data: null,
  error: error instanceof ApiError ? error.message : 'Не удалось загрузить данные. Повторите попытку.' });

export function useConceptSelection() {
  const [projectId, setProjectId] = useState<string | null>(null);
  const [conceptVariantId, setConceptId] = useState<string | null>(null);
  const projectRef = useRef<string | null>(null);
  const conceptRef = useRef<string | null>(null);
  const [projects, setProjects] = useState<Resource<ProjectResponse[]>>(loading('projects'));
  const [variants, setVariants] = useState<Resource<ConceptVariantSummaryResponse[]>>(idle);
  const [details, setDetails] = useState<Resource<ConceptVariantDetailResponse>>(idle);
  const [projectAttempt, retryProjectRequest] = useState(0);
  const [variantAttempt, retryVariantRequest] = useState(0);
  const [detailAttempt, retryDetailRequest] = useState(0);

  const selectProject = useCallback((id: string | null) => {
    projectRef.current = id;
    conceptRef.current = null;
    setProjectId(id);
    setConceptId(null);
    setVariants(id ? loading(id) : idle());
    setDetails(idle());
    retryVariantRequest(n => n + 1);
  }, []);
  const selectConcept = useCallback((id: string | null) => {
    conceptRef.current = id;
    setConceptId(id);
    setDetails(id ? loading(id) : idle());
    retryDetailRequest(n => n + 1);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    let current = true;
    setProjects(loading('projects'));
    getProjects(controller.signal).then(data => {
      if (!current) return;
      setProjects({ key: 'projects', status: 'success', data, error: null });
      if (data.length === 1) selectProject(data[0].id);
    }).catch((error: unknown) => {
      if (current && !isAbortError(error)) setProjects(failure('projects', error));
    });
    return () => { current = false; controller.abort(); };
  }, [projectAttempt, selectProject]);

  useEffect(() => {
    if (!projectId) return;
    const controller = new AbortController();
    let current = true;
    setVariants(loading(projectId));
    getProjectVariants(projectId, controller.signal).then(data => {
      if (!current || projectRef.current !== projectId) return;
      setVariants({ key: projectId, status: 'success', data, error: null });
      selectConcept(data[0]?.id ?? null);
    }).catch((error: unknown) => {
      if (current && projectRef.current === projectId && !isAbortError(error)) setVariants(failure(projectId, error));
    });
    return () => { current = false; controller.abort(); };
  }, [projectId, variantAttempt, selectConcept]);

  useEffect(() => {
    if (!conceptVariantId || !projectId) return;
    const controller = new AbortController();
    let current = true;
    setDetails(loading(conceptVariantId));
    getConceptDetails(conceptVariantId, controller.signal).then(data => {
      if (!current || projectRef.current !== projectId || conceptRef.current !== conceptVariantId) return;
      if (data.projectId.toLowerCase() !== projectId.toLowerCase()) throw new ApiError('invalid-response', 'Концепция не принадлежит выбранному проекту. Обновите список.');
      setDetails({ key: conceptVariantId, status: 'success', data, error: null });
    }).catch((error: unknown) => {
      if (current && projectRef.current === projectId && conceptRef.current === conceptVariantId && !isAbortError(error))
        setDetails(failure(conceptVariantId, error));
    });
    return () => { current = false; controller.abort(); };
  }, [projectId, conceptVariantId, detailAttempt]);

  const retryProjects = () => { selectProject(null); setProjects(loading('projects')); retryProjectRequest(n => n + 1); };
  const retryVariants = () => { selectConcept(null); if (projectId) setVariants(loading(projectId)); retryVariantRequest(n => n + 1); };
  const retryDetails = () => { if (conceptVariantId) setDetails(loading(conceptVariantId)); retryDetailRequest(n => n + 1); };
  // Keys protect rendering even between a new selection and effect cleanup.
  const currentVariants = variants.key === projectId ? variants : projectId ? loading<ConceptVariantSummaryResponse[]>(projectId) : idle<ConceptVariantSummaryResponse[]>();
  const currentDetails = details.key === conceptVariantId ? details : conceptVariantId ? loading<ConceptVariantDetailResponse>(conceptVariantId) : idle<ConceptVariantDetailResponse>();
  return { projectId, conceptVariantId, projects, variants: currentVariants, details: currentDetails,
    selectedProject: projects.data?.find(project => project.id === projectId) ?? null,
    selectedConcept: currentVariants.data?.find(concept => concept.id === conceptVariantId) ?? null,
    selectProject, selectConcept, retryProjects, retryVariants, retryDetails };
}
