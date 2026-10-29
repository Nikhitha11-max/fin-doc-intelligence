import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { platformAdminGuard, tenantAdminGuard } from './core/role.guard';
import { DocumentsComponent } from './documents/documents.component';
import { LoginComponent } from './login/login.component';
import { PlatformComponent } from './platform/platform.component';
import { TeamComponent } from './team/team.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'documents', component: DocumentsComponent, canActivate: [authGuard] },
  { path: 'team', component: TeamComponent, canActivate: [authGuard, tenantAdminGuard] },
  { path: 'platform', component: PlatformComponent, canActivate: [authGuard, platformAdminGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'documents' },
  { path: '**', redirectTo: 'documents' },
];
