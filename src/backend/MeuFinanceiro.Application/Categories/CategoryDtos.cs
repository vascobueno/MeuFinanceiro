using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Categories;

public record CreateCategoryRequest(string Name, CategoryType Type);
public record UpdateCategoryRequest(string Name);
public record CategoryResponse(Guid Id, string Name, CategoryType Type, bool IsSystemDefault, IReadOnlyList<SubcategoryResponse> Subcategories);

public record CreateSubcategoryRequest(string Name);
public record SubcategoryResponse(Guid Id, string Name);
