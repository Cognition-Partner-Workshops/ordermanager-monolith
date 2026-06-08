import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-inventory-list',
  standalone: true,
  imports: [CommonModule],
  template: `
    <h2>Inventory</h2>
    <p class="info">Data served by inventory-service microservice</p>
    <table *ngIf="items.length">
      <thead><tr><th>Product</th><th>SKU</th><th>On Hand</th><th>Reorder Level</th><th>Location</th><th>Last Restocked</th></tr></thead>
      <tbody>
        <tr *ngFor="let i of items" [class.low-stock]="i.quantityOnHand <= i.reorderLevel">
          <td>{{i.productName}}</td><td>{{i.sku}}</td><td>{{i.quantityOnHand}}</td><td>{{i.reorderLevel}}</td><td>{{i.warehouseLocation}}</td><td>{{i.lastRestocked | date}}</td>
        </tr>
      </tbody>
    </table>
    <p *ngIf="!items.length">No inventory items found.</p>
  `,
  styles: [`
    .info { color: #666; font-style: italic; margin-bottom: 1rem; }
    .low-stock { background-color: #ffe0e0; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }
    th { background-color: #f5f5f5; font-weight: bold; }
  `]
})
export class InventoryListComponent implements OnInit {
  items: any[] = [];
  constructor(private http: HttpClient) {}
  ngOnInit() {
    this.http.get<any[]>(`${environment.inventoryApiUrl}/api/inventory`).subscribe(data => this.items = data);
  }
}
