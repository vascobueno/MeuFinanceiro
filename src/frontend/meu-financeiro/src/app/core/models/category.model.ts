export enum CategoryType {
  Income = 1,
  Expense = 2,
}

export interface Subcategory {
  id: string;
  name: string;
}

export interface Category {
  id: string;
  name: string;
  type: CategoryType;
  isSystemDefault: boolean;
  subcategories: Subcategory[];
}

export interface CreateCategoryRequest {
  name: string;
  type: CategoryType;
}
