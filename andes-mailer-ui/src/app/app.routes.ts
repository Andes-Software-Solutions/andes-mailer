import { Routes } from '@angular/router';
import { routePaths } from '@core/navigation/route-paths';
import { Home } from '@features/home/home';

export const routes: Routes = [
  { path: routePaths.home, component: Home, title: 'Andes Mailer', pathMatch: 'full' },
];
