import type { ProjectResponse, ConceptVariantSummaryResponse, ConceptVariantDetailResponse } from './types.ts';

import { ApiError, isGuid, invalidResponse, requestJson as request } from './http.ts';
export { ApiError, isAbortError } from './http.ts';
const sameId = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();
const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null && !Array.isArray(value);
const isName = (value: unknown): value is string => typeof value === 'string' && value.trim().length > 0;
const isArea = (value: unknown): value is number => typeof value === 'number' && Number.isFinite(value) && value >= 0;
const isCount = (value: unknown): value is number => isArea(value) && Number.isInteger(value) && value <= 2147483647;
const optional = (value: unknown, check: (v: unknown) => boolean) => value === null || check(value);
const isProject = (value: unknown): value is ProjectResponse => isRecord(value) && isGuid(value.id) && isName(value.name)
  && typeof value.createdAt === 'string' && /(?:Z|\+00:00)$/.test(value.createdAt) && Number.isFinite(Date.parse(value.createdAt));
const isSummary = (value: unknown): value is ConceptVariantSummaryResponse => isRecord(value) && isGuid(value.id)
  && isGuid(value.projectId) && isName(value.name) && isArea(value.residentialArea) && isArea(value.commercialArea) && isCount(value.parkingSpaces);
const isDetail = (value: unknown): value is ConceptVariantDetailResponse => isRecord(value) && isSummary(value)
  && ['constructionResidentialArea', 'constructionCommercialArea', 'socialAreas'].every(key => optional(value[key], isArea))
  && ['residentsCount', 'buildingsCount', 'maxFloors', 'preschoolPlaces', 'schoolPlaces'].every(key => optional(value[key], isCount));
function listOf<T extends { id: string }>(check: (v: unknown) => v is T): (v: unknown) => v is T[] {
  return (value: unknown): value is T[] => Array.isArray(value) && value.every(check)
    && new Set(value.map(item => item.id.toLowerCase())).size === value.length;
}
function requireId(id: string) {
  if (!isGuid(id)) throw new ApiError('invalid-request', 'Сначала выберите проект или концепцию.');
}

export const getProjects = (signal?: AbortSignal): Promise<ProjectResponse[]> => request('/projects', listOf(isProject), signal);
export async function getProject(id: string, signal?: AbortSignal): Promise<ProjectResponse> {
  requireId(id);
  const project = await request(`/projects/${encodeURIComponent(id)}`, isProject, signal);
  if (!sameId(project.id, id)) throw invalidResponse();
  return project;
}
export async function getProjectVariants(id: string, signal?: AbortSignal): Promise<ConceptVariantSummaryResponse[]> {
  requireId(id);
  const variants = await request(`/projects/${encodeURIComponent(id)}/variants`, listOf(isSummary), signal);
  if (variants.some(variant => !sameId(variant.projectId, id))) throw invalidResponse();
  return variants;
}
export async function getConceptDetails(id: string, signal?: AbortSignal): Promise<ConceptVariantDetailResponse> {
  requireId(id);
  const concept = await request(`/concepts/${encodeURIComponent(id)}`, isDetail, signal);
  if (!sameId(concept.id, id)) throw invalidResponse();
  return concept;
}
