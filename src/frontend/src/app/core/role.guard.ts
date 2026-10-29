import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AccessService } from './access.service';

// UX convenience only; the API enforces these rules on every request.
export const tenantAdminGuard: CanActivateFn = () => {
  const router = inject(Router);
  return inject(AccessService).loadMe().pipe(map((me) => (me.role === 'TenantAdmin' ? true : router.createUrlTree(['/documents']))));
};

export const platformAdminGuard: CanActivateFn = () => {
  const router = inject(Router);
  return inject(AccessService).loadMe().pipe(map((me) => (me.isPlatformAdmin ? true : router.createUrlTree(['/documents']))));
};
