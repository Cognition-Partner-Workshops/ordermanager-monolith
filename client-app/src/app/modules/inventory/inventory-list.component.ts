import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

interface InventoryItem {
  productId: number;
  productName: string;
  productSku: string;
  quantityOnHand: number;
  reorderLevel: number;
  warehouseLocation: string;
  lastRestocked: string;
  isLowStock: boolean;
}

@Component({
  selector: 'app-inventory-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <h2>Inventory</h2>
    <p class="hint">Stock levels are served by the inventory-service.</p>
    <p *ngIf="error" class="error">{{error}}</p>
    <table *ngIf="items.length">
      <thead><tr><th>Product</th><th>SKU</th><th>On Hand</th><th>Reorder Level</th><th>Location</th><th>Last Restocked</th><th>Restock</th></tr></thead>
      <tbody>
        <tr *ngFor="let i of items" [class.low-stock]="i.isLowStock">
          <td>{{i.productName}}</td><td>{{i.productSku}}</td><td>{{i.quantityOnHand}}</td><td>{{i.reorderLevel}}</td><td>{{i.warehouseLocation}}</td><td>{{i.lastRestocked | date}}</td>
          <td>
            <input type="number" min="1" [(ngModel)]="restockQty[i.productId]" style="width:4rem">
            <button type="button" (click)="restock(i)" [disabled]="!(restockQty[i.productId] > 0)">Add</button>
          </td>
        </tr>
      </tbody>
    </table>
  `
})
export class InventoryListComponent implements OnInit {
  items: InventoryItem[] = [];
  restockQty: Record<number, number> = {};
  error = '';
  constructor(private http: HttpClient) {}
  ngOnInit() { this.load(); }
  load() {
    this.http.get<InventoryItem[]>('/api/inventory').subscribe({
      next: data => this.items = data,
      error: err => this.error = err?.error?.error ?? 'Inventory service unavailable'
    });
  }
  restock(item: InventoryItem) {
    const quantity = this.restockQty[item.productId];
    this.http.post<InventoryItem>(`/api/inventory/product/${item.productId}/restock`, { quantity }).subscribe({
      next: updated => { this.items = this.items.map(i => i.productId === updated.productId ? updated : i); this.restockQty[item.productId] = 0; },
      error: err => this.error = err?.error?.error ?? 'Restock failed'
    });
  }
}
