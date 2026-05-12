import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

interface Project {
  id?: number;
  name: string;
  description: string;
  customerId: number;
  customerName?: string;
  startDate: string;
  status: string;
}

interface Customer {
  id: number;
  name: string;
}

@Component({
  selector: 'app-project-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <h2>Projects</h2>

    <div class="form-section">
      <h3>{{ editingProject ? 'Edit Project' : 'Create Project' }}</h3>
      <form (ngSubmit)="saveProject()">
        <label>Name: <input [(ngModel)]="form.name" name="name" required /></label>
        <label>Description: <input [(ngModel)]="form.description" name="description" /></label>
        <label>Client:
          <select [(ngModel)]="form.customerId" name="customerId" required>
            <option [ngValue]="0" disabled>Select a client</option>
            <option *ngFor="let c of customers" [ngValue]="c.id">{{c.name}}</option>
          </select>
        </label>
        <label>Start Date: <input type="date" [(ngModel)]="form.startDate" name="startDate" required /></label>
        <label>Status:
          <select [(ngModel)]="form.status" name="status" required>
            <option value="Active">Active</option>
            <option value="Completed">Completed</option>
            <option value="On-Hold">On-Hold</option>
          </select>
        </label>
        <div class="form-actions">
          <button type="submit">{{ editingProject ? 'Update' : 'Create' }}</button>
          <button type="button" *ngIf="editingProject" (click)="cancelEdit()">Cancel</button>
        </div>
      </form>
    </div>

    <table *ngIf="projects.length">
      <thead>
        <tr>
          <th>Name</th>
          <th>Description</th>
          <th>Client</th>
          <th>Start Date</th>
          <th>Status</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let p of projects">
          <td>{{p.name}}</td>
          <td>{{p.description}}</td>
          <td>{{p.customer?.name ?? 'N/A'}}</td>
          <td>{{p.startDate | date:'mediumDate'}}</td>
          <td>{{p.status}}</td>
          <td>
            <button (click)="editProject(p)">Edit</button>
            <button (click)="deleteProject(p.id!)">Delete</button>
          </td>
        </tr>
      </tbody>
    </table>
    <p *ngIf="!projects.length">No projects found.</p>
  `
})
export class ProjectListComponent implements OnInit {
  projects: any[] = [];
  customers: Customer[] = [];
  editingProject: any = null;
  form: Project = { name: '', description: '', customerId: 0, startDate: '', status: 'Active' };

  constructor(private http: HttpClient) {}

  ngOnInit() {
    this.loadProjects();
    this.http.get<Customer[]>('/api/customers').subscribe(data => this.customers = data);
  }

  loadProjects() {
    this.http.get<any[]>('/api/projects').subscribe(data => this.projects = data);
  }

  saveProject() {
    if (this.editingProject) {
      this.http.put(`/api/projects/${this.editingProject.id}`, this.form).subscribe(() => {
        this.loadProjects();
        this.cancelEdit();
      });
    } else {
      this.http.post('/api/projects', this.form).subscribe(() => {
        this.loadProjects();
        this.resetForm();
      });
    }
  }

  editProject(project: any) {
    this.editingProject = project;
    this.form = {
      name: project.name,
      description: project.description,
      customerId: project.customerId,
      startDate: project.startDate?.substring(0, 10) ?? '',
      status: project.status
    };
  }

  cancelEdit() {
    this.editingProject = null;
    this.resetForm();
  }

  deleteProject(id: number) {
    this.http.delete(`/api/projects/${id}`).subscribe(() => this.loadProjects());
  }

  resetForm() {
    this.form = { name: '', description: '', customerId: 0, startDate: '', status: 'Active' };
  }
}
