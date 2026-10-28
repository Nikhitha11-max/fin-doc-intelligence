import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { DocumentsComponent } from './documents/documents.component';
import { LoginComponent } from './login/login.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'documents', component: DocumentsComponent, canActivate: [authGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'documents' },
  { path: '**', redirectTo: 'documents' },
];
