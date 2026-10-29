import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { PagedResult } from '../documents/documents.service';

export type TenantRole = 'TenantAdmin' | 'DocumentManager' | 'Analyst' | 'Auditor';
export const ROLES: TenantRole[] = ['TenantAdmin', 'DocumentManager', 'Analyst', 'Auditor'];

export interface Me {
  hasAccess: boolean;
  tenantName: string | null;
  role: TenantRole | null;
  isPlatformAdmin: boolean;
}

export interface Member {
  userId: string;
  email: string;
  role: TenantRole;
  createdAt: string;
}

export interface TenantSummary {
  id: string;
  name: string;
  isActive: boolean;
  createdAt: string;
}

// Maps an API error to a short, non-technical message.
export function describeError(e: HttpErrorResponse, fallback: string): string {
  if (e.status === 403) return 'You do not have permission to do that.';
  if (e.status === 404) return 'That item no longer exists.';
  if ((e.status === 400 || e.status === 409) && typeof e.error?.detail === 'string') return e.error.detail;
  return fallback;
}

@Injectable({ providedIn: 'root' })
export class AccessService {
  private readonly http = inject(HttpClient);

  readonly me = signal<Me | null>(null);

  loadMe(): Observable<Me> {
    return this.http.get<Me>('/api/me').pipe(tap((me) => this.me.set(me)));
  }

  listMembers(page: number, pageSize: number): Observable<PagedResult<Member>> {
    return this.http.get<PagedResult<Member>>('/api/members', { params: { page, pageSize } });
  }

  addMember(email: string, password: string, role: TenantRole): Observable<Member> {
    return this.http.post<Member>('/api/members', { email, password, role });
  }

  changeRole(userId: string, role: TenantRole): Observable<Member> {
    return this.http.put<Member>(`/api/members/${encodeURIComponent(userId)}/role`, { role });
  }

  removeMember(userId: string): Observable<void> {
    return this.http.delete<void>(`/api/members/${encodeURIComponent(userId)}`);
  }

  listTenants(page: number, pageSize: number): Observable<PagedResult<TenantSummary>> {
    return this.http.get<PagedResult<TenantSummary>>('/api/platform/tenants', { params: { page, pageSize } });
  }

  createTenant(name: string, adminEmail: string, adminPassword: string): Observable<TenantSummary> {
    return this.http.post<TenantSummary>('/api/platform/tenants', { name, adminEmail, adminPassword });
  }

  renameTenant(id: string, name: string): Observable<TenantSummary> {
    return this.http.put<TenantSummary>(`/api/platform/tenants/${id}`, { name });
  }

  setTenantActive(id: string, active: boolean): Observable<TenantSummary> {
    return this.http.post<TenantSummary>(`/api/platform/tenants/${id}/${active ? 'reactivate' : 'deactivate'}`, {});
  }
}
