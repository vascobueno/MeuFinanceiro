import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule, MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { AuthService } from '../../core/services/auth.service';
import { CategoryService } from '../../core/services/category.service';
import { Category, CategoryType } from '../../core/models/category.model';
import { ListStateComponent } from '../../shared/components/list-state.component';

@Component({
  selector: 'mf-category-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ListStateComponent,
  ],
  providers: [{ provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { floatLabel: 'always', subscriptSizing: 'dynamic' } }],
  templateUrl: './category-list.component.html',
  styleUrl: './category-list.component.scss',
})
export class CategoryListComponent implements OnInit {
  readonly CategoryType = CategoryType;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly categories = signal<Category[]>([]);

  newName = '';
  newType: CategoryType = CategoryType.Expense;
  readonly saving = signal(false);

  readonly subcategoryDrafts = new Map<string, string>();

  constructor(
    private readonly categoryService: CategoryService,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.categoryService.getAll().subscribe({
      next: (categories) => {
        this.categories.set(categories);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar as categorias.');
        this.loading.set(false);
      },
    });
  }

  addCategory(): void {
    if (!this.newName.trim()) return;

    this.saving.set(true);
    this.categoryService.create({ name: this.newName, type: this.newType }).subscribe({
      next: () => {
        this.saving.set(false);
        this.newName = '';
        this.load();
      },
      error: () => this.saving.set(false),
    });
  }

  addSubcategory(category: Category): void {
    const name = this.subcategoryDrafts.get(category.id)?.trim();
    if (!name) return;

    this.categoryService.addSubcategory(category.id, name).subscribe({
      next: () => {
        this.subcategoryDrafts.set(category.id, '');
        this.load();
      },
    });
  }

  deleteCategory(category: Category): void {
    this.categoryService.delete(category.id).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Não foi possível excluir uma categoria já usada em lançamentos.'),
    });
  }
}
