import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-inventory-list',
  standalone: true,
  imports: [CommonModule],
  template: `
    <h2>Inventory</h2>
    <p *ngIf="!items.length">Loading inventory from microservice...</p>
    <table *ngIf="items.length">
      <thead><tr><th>Product</th><th>SKU</th><th>On Hand</th><th>Reorder Level</th><th>Location</th><th>Last Restocked</th></tr></thead>
      <tbody>
        <tr *ngFor="let i of items" [class.low-stock]="i.quantityOnHand <= i.reorderLevel">
          <td>{{i.productName}}</td><td>{{i.productSku}}</td><td>{{i.quantityOnHand}}</td><td>{{i.reorderLevel}}</td><td>{{i.warehouseLocation}}</td><td>{{i.lastRestocked | date}}</td>
        </tr>
      </tbody>
    </table>
  `
})
export class InventoryListComponent implements OnInit {
  items: any[] = [];
  constructor(private http: HttpClient) {}
  ngOnInit() {
    // Inventory data now comes from the inventory-service microservice.
    // In production, this is routed via ingress/proxy.
    // For local dev, the monolith proxies /api/inventory to the inventory-service.
    this.http.get<any[]>('/api/inventory').subscribe(data => this.items = data);
  }
}
