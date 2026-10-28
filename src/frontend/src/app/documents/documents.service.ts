import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface DocumentItem {
  id: string;
  title: string;
  fileName: string;
  extension: string;
  sizeBytes: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export const MAX_UPLOAD_BYTES = 25 * 1024 * 1024;
export const ALLOWED_EXTENSIONS = ['.pdf', '.csv', '.xlsx'];

@Injectable({ providedIn: 'root' })
export class DocumentsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/documents';

  list(page: number, pageSize: number): Observable<PagedResult<DocumentItem>> {
    return this.http.get<PagedResult<DocumentItem>>(this.baseUrl, { params: { page, pageSize } });
  }

  upload(file: File, title: string): Observable<DocumentItem> {
    const form = new FormData();
    form.append('file', file);
    if (title.trim()) {
      form.append('title', title.trim());
    }
    return this.http.post<DocumentItem>(this.baseUrl, form);
  }

  rename(id: string, title: string): Observable<DocumentItem> {
    return this.http.put<DocumentItem>(`${this.baseUrl}/${id}`, { title });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  download(id: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${id}/download`, { responseType: 'blob' });
  }
}
