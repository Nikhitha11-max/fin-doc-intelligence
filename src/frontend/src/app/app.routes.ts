import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { auditViewerGuard, platformAdminGuard, tenantAdminGuard } from './core/role.guard';
import { AuditComponent } from './audit/audit.component';
import { DocumentsComponent } from './documents/documents.component';
import { LoginComponent } from './login/login.component';
import { PlatformComponent } from './platform/platform.component';
import { TeamComponent } from './team/team.component';
import { SearchComponent } from './search/search.component';
import { AnswersComponent } from './answers/answers.component';
import { HistoryComponent } from './history/history.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'documents', component: DocumentsComponent, canActivate: [authGuard] },
  { path: 'search', component: SearchComponent, canActivate: [authGuard] },
  { path: 'answers', component: AnswersComponent, canActivate: [authGuard] },
  { path: 'history', component: HistoryComponent, canActivate: [authGuard] },
  { path: 'audit', component: AuditComponent, canActivate: [authGuard, auditViewerGuard] },
  { path: 'team', component: TeamComponent, canActivate: [authGuard, tenantAdminGuard] },
  { path: 'platform', component: PlatformComponent, canActivate: [authGuard, platformAdminGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'documents' },
  { path: '**', redirectTo: 'documents' },
];
