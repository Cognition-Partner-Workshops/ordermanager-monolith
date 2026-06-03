import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-inventory-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <h2>Inventory</h2>
    <div *ngIf="error" class="error" style="color:red;margin-bottom:8px">{{ error }}</div>
    <p *ngIf="loading">Loading inventory...</p>
    <table *ngIf="items.length">
      <thead><tr><th>Product</th><th>On Hand</th><th>Reorder Level</th><th>Location</th><th>Last Restocked</th><th>Actions</th></tr></thead>
      <tbody>
        <tr *ngFor="let i of items" [class.low-stock]="i.quantityOnHand <= i.reorderLevel">
          <td>{{i.productName || i.product?.name}}</td>
          <td>{{i.quantityOnHand}}</td>
          <td>{{i.reorderLevel}}</td>
          <td>{{i.warehouseLocation}}</td>
          <td>{{i.lastRestocked | date}}</td>
          <td>
            <input type="number" [(ngModel)]="i.restockQty" min="1" placeholder="Qty" style="width:60px" />
            <button (click)="restock(i)">Restock</button>
          </td>
        </tr>
      </tbody>
    </table>
    <p *ngIf="!items.length && !error && !loading">No inventory items found.</p>
  `
})
export class InventoryListComponent implements OnInit {
  items: any[] = [];
  error = '';
  loading = true;

  constructor(private http: HttpClient) {}

  ngOnInit() {
    this.http.get<any[]>('/api/inventory').subscribe({
      next: data => {
        this.items = data.map(i => ({ ...i, restockQty: 10 }));
        this.loading = false;
      },
      error: () => {
        this.error = 'Unable to reach inventory service';
        this.loading = false;
      }
    });
  }

  restock(item: any) {
    this.error = '';
    this.http.post<any>(`/api/inventory/product/${item.productId}/restock`, { quantity: item.restockQty }).subscribe({
      next: updated => {
        item.quantityOnHand = updated.quantityOnHand;
        item.lastRestocked = updated.lastRestocked;
      },
      error: () => this.error = 'Restock failed'
    });
  }
}
