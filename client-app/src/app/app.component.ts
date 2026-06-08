import { Component } from '@angular/core';
import { RouterOutlet, RouterLink } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink],
  template: `
    <nav>
      <h1>OrderManager</h1>
      <a routerLink="/orders">Orders</a>
      <a routerLink="/products">Products</a>
      <a routerLink="/customers">Customers</a>
    </nav>
    <router-outlet></router-outlet>
  `
})
export class AppComponent {
  title = 'OrderManager';
}
