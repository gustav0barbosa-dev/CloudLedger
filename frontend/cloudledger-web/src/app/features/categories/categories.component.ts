import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../core/api-client.service';
import { ToastService } from '../../core/toast.service';

interface Category {
  id: string;
  name: string;
  kind: 'Expense' | 'Income';
  isDefault: boolean;
  color?: string;
  icon?: string;
}

@Component({
  selector: 'app-categories',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="page-head">
      <h1>Categorias</h1>
      <select class="period" [(ngModel)]="kindFilter" (ngModelChange)="load()">
        <option value="">Todas</option>
        <option value="Expense">Despesas</option>
        <option value="Income">Receitas</option>
      </select>
    </div>

    <div class="card">
      <div class="card-head"><strong>{{ editingId() ? 'Editar categoria' : 'Nova categoria' }}</strong></div>
      <div class="form-row">
        <input [(ngModel)]="name" placeholder="Nome da categoria" />
        @if (!editingId()) {
          <select [(ngModel)]="kind">
            <option value="Expense">Despesa</option>
            <option value="Income">Receita</option>
          </select>
        }
        <input [(ngModel)]="color" type="color" title="Cor" />
        <input [(ngModel)]="icon" placeholder="Ícone (opcional)" />
        <button class="btn" (click)="save()">{{ editingId() ? 'Salvar' : 'Criar' }}</button>
        @if (editingId()) {
          <button class="btn secondary" (click)="reset()">Cancelar</button>
        }
      </div>
    </div>

    <div class="card" style="margin-top:12px">
      <table class="table">
        <thead><tr><th>Categoria</th><th>Tipo</th><th>Origem</th><th></th></tr></thead>
        <tbody>
          @for (category of categories(); track category.id) {
            <tr>
              <td>
                <span class="swatch" [style.background]="category.color || '#8b5cf6'"></span>
                {{ category.name }}
              </td>
              <td>{{ category.kind === 'Expense' ? 'Despesa' : 'Receita' }}</td>
              <td>{{ category.isDefault ? 'Padrão' : 'Personalizada' }}</td>
              <td>
                @if (!category.isDefault) {
                  <button class="btn secondary small" (click)="edit(category)">Editar</button>
                  <button class="btn danger small" (click)="remove(category)">Excluir</button>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
      @if (!categories().length) { <div class="empty">Nenhuma categoria encontrada.</div> }
    </div>
  `,
  styles: [`
    .swatch { display:inline-block; width:10px; height:10px; border-radius:50%; margin-right:8px; vertical-align:middle; }
    .small { padding:5px 9px; font-size:11px; margin-right:5px; }
  `]
})
export class CategoriesComponent implements OnInit {
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);

  readonly categories = signal<Category[]>([]);
  readonly editingId = signal<string | null>(null);

  name = '';
  kind: 'Expense' | 'Income' = 'Expense';
  color = '#8b5cf6';
  icon = '';
  kindFilter = '';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.api.get<Category[]>('/categories', { kind: this.kindFilter || undefined }).subscribe({
      next: items => this.categories.set(items)
    });
  }

  save(): void {
    const id = this.editingId();
    const request = id
      ? { name: this.name, color: this.color, icon: this.icon || null }
      : { name: this.name, kind: this.kind, color: this.color, icon: this.icon || null };

    const operation = id
      ? this.api.put<Category>(`/categories/${id}`, request)
      : this.api.post<Category>('/categories', request);

    operation.subscribe({
      next: () => {
        this.toasts.success(id ? 'Categoria atualizada.' : 'Categoria criada.');
        this.reset();
        this.load();
      }
    });
  }

  edit(category: Category): void {
    this.editingId.set(category.id);
    this.name = category.name;
    this.color = category.color || '#8b5cf6';
    this.icon = category.icon || '';
  }

  reset(): void {
    this.editingId.set(null);
    this.name = '';
    this.color = '#8b5cf6';
    this.icon = '';
  }

  remove(category: Category): void {
    if (!confirm(`Excluir a categoria "${category.name}"?`)) {
      return;
    }
    this.api.delete<void>(`/categories/${category.id}`).subscribe({
      next: () => {
        this.toasts.success('Categoria excluída.');
        this.load();
      }
    });
  }
}
