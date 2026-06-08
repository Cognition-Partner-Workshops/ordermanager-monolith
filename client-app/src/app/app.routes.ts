import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: '/orders', pathMatch: 'full' },
  { path: 'orders', loadComponent: () => import('./modules/orders/order-list.component').then(m => m.OrderListComponent) },
  { path: 'products', loadComponent: () => import('./modules/products/product-list.component').then(m => m.ProductListComponent) },
  { path: 'customers', loadComponent: () => import('./modules/customers/customer-list.component').then(m => m.CustomerListComponent) },
];
