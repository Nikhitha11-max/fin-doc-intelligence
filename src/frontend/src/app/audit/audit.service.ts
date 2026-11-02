import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface AuditEvent {
  id: string;
  actorUserId: string;
  action: string;
  documentId: string | null;
  targetUserId: string | null;
  detail: string | null;
  occurredAt: string;
}

export interface AuditPage {
  items: AuditEvent[];
  total: number;
  page: number;
  pageSize: number;
}

export interface AuditFilters {
  from?: string;
  to?: string;
  actor?: string;
  action?: string;
}

@Injectable({ providedIn: 'root' })
export class AuditService {
  private readonly http = inject(HttpClient);

  list(filters: AuditFilters, page: number, pageSize = 50): Observable<AuditPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    for (const [key, value] of Object.entries(filters)) {
      if (value) params = params.set(key, value);
    }
    return this.http.get<AuditPage>('/api/audit', { params });
  }
}
