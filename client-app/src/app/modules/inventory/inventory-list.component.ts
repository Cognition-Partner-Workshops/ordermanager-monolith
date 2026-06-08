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
    <div class="controls">
      <label><input type="checkbox" [(ngModel)]="showLowStockOnly" (change)="loadInventory()"> Show Low Stock Only</label>
    </div>
    <table *ngIf="items.length">
      <thead><tr><th>Product</th><th>On Hand</th><th>Reorder Level</th><th>Location</th><th>Last Restocked</th><th>Actions</th></tr></thead>
      <tbody>
        <tr *ngFor="let i of items" [class.low-stock]="i.quantityOnHand <= i.reorderLevel">
          <td>{{i.productName || i.product?.name}}</td><td>{{i.quantityOnHand}}</td><td>{{i.reorderLevel}}</td><td>{{i.warehouseLocation}}</td><td>{{i.lastRestocked | date}}</td>
          <td><button (click)="restock(i.productId, 50)">Restock +50</button></td>
        </tr>
      </tbody>
    </table>
  `
})
export class InventoryListComponent implements OnInit {
  items: any[] = [];
  showLowStockOnly = false;
  private apiUrl = environment.inventoryApiUrl || '/api/inventory';

  constructor(private http: HttpClient) {}

  ngOnInit() { this.loadInventory(); }

  loadInventory() {
    const url = this.showLowStockOnly ? `${this.apiUrl}/low-stock` : this.apiUrl;
    this.http.get<any[]>(url).subscribe(data => this.items = data);
  }

  restock(productId: number, quantity: number) {
    this.http.post<any>(`${this.apiUrl}/product/${productId}/restock`, { quantity })
      .subscribe(() => this.loadInventory());
  }
}
