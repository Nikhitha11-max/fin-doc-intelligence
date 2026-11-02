import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface SearchResult {
  documentId: string;
  documentTitle: string;
  chunkId: string;
  ordinal: number;
  pageStart: number | null;
  pageEnd: number | null;
  sheetName: string | null;
  rowStart: number | null;
  rowEnd: number | null;
  score: number;
  snippet: string;
}

export interface SearchResponse {
  results: SearchResult[];
}

@Injectable({ providedIn: 'root' })
export class SearchService {
  private readonly http = inject(HttpClient);

  search(query: string, limit = 10): Observable<SearchResponse> {
    return this.http.post<SearchResponse>('/api/search', { query, limit });
  }
}
