export interface ProjectResponse {
  id: string;
  name: string;
  createdAt: string;
}

export interface ConceptVariantSummaryResponse {
  id: string;
  projectId: string;
  name: string;
  residentialArea: number;
  commercialArea: number;
  parkingSpaces: number;
}

export interface ConceptVariantDetailResponse extends ConceptVariantSummaryResponse {
  constructionResidentialArea: string | null;
  constructionCommercialArea: string | null;
  residentsCount: number | null;
  buildingsCount: number | null;
  maxFloors: number | null;
  socialAreas: number | null;
  preschoolPlaces: number | null;
  schoolPlaces: number | null;
}
