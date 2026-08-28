using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Categories;

public class CategoryService : ICategoryService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CategoryService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CategoryResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .Include(c => c.Subcategories)
            .Where(c => c.HouseholdId == _currentUser.HouseholdId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        return categories.Select(ToResponse).ToList();
    }

    public async Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        var category = new Category
        {
            HouseholdId = _currentUser.HouseholdId,
            Name = request.Name,
            Type = request.Type,
            CreatedBy = _currentUser.UserId
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return ToResponse(category);
    }

    public async Task<CategoryResponse> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var category = await FindOwnedAsync(id, ct);
        category.Name = request.Name;
        category.UpdatedAt = DateTime.UtcNow;
        category.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return ToResponse(category);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var category = await FindOwnedAsync(id, ct);

        var inUse = await _db.FinancialTransactions.AnyAsync(t => t.CategoryId == id, ct);
        if (inUse)
            throw new BusinessRuleException("categoria-em-uso", "Não é possível excluir uma categoria já usada em lançamentos.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<SubcategoryResponse> AddSubcategoryAsync(Guid categoryId, CreateSubcategoryRequest request, CancellationToken ct = default)
    {
        await FindOwnedAsync(categoryId, ct);

        var subcategory = new Subcategory { CategoryId = categoryId, Name = request.Name };
        _db.Subcategories.Add(subcategory);
        await _db.SaveChangesAsync(ct);

        return new SubcategoryResponse(subcategory.Id, subcategory.Name);
    }

    private async Task<Category> FindOwnedAsync(Guid id, CancellationToken ct)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException(nameof(Category), id);
        if (category.HouseholdId != _currentUser.HouseholdId) throw new ForbiddenException();
        return category;
    }

    private static CategoryResponse ToResponse(Category c) => new(
        c.Id, c.Name, c.Type, c.IsSystemDefault,
        c.Subcategories.Select(s => new SubcategoryResponse(s.Id, s.Name)).ToList());
}
