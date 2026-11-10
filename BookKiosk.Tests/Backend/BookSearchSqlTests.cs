using BookKiosk.Domain.Entities;
using BookKiosk.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.Tests.Backend;

public sealed class SqlQaTheoryAttribute : TheoryAttribute
{
    public SqlQaTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOOKKIOSK_QA_SQL_MASTER")))
            Skip = "SQL acceptance requires an approved disposable SQL QA connection.";
    }
}

public sealed class BookSearchSqlTests(DisposableSqlQa db) : IClassFixture<DisposableSqlQa>
{
    [SqlQaTheory]
    [InlineData("Title")]
    [InlineData("Author")]
    [InlineData("Barcode")]
    [InlineData("Publisher")]
    [InlineData("Category")]
    [InlineData("Area")]
    public async Task Each_search_field_is_case_insensitive_trimmed_filtered_and_active_only(string field)
    {
        await using var context = db.Context();
        var term = "needle_" + Guid.NewGuid().ToString("N");
        var book = Book("Default title");
        switch (field)
        {
            case "Title": book.Title = term; break;
            case "Author": book.Author = term; break;
            case "Barcode": book.Barcode = term; break;
            case "Publisher": book.Publisher = term; break;
            case "Category": book.Category!.Name = term; break;
            case "Area": book.Area = new Area { Name = term }; break;
        }
        var inactive = Book(term); inactive.IsActive = false; inactive.Category = book.Category;
        context.Books.AddRange(book, inactive); await context.SaveChangesAsync();
        var repository = new BookRepository(context);
        var result = await repository.GetPagedAsync(1, 12, "  " + term.ToUpperInvariant() + "  ", book.CategoryId);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(book.BookId, Assert.Single(result.Items).BookId);
        Assert.Empty((await repository.GetPagedAsync(1, 12, term + "no_match", null)).Items);
        Assert.Empty((await repository.GetPagedAsync(1, 12, term, int.MaxValue)).Items);
        Assert.Equal(0, (await repository.GetPagedAsync(1, 12, new string('x', 501), null)).TotalCount);
    }

    [SqlQaFact]
    public async Task Pagination_has_stable_title_id_order_no_duplicates_and_safe_extreme_page()
    {
        await using var context = db.Context();
        var term = "pages_" + Guid.NewGuid().ToString("N");
        var a = Book(term + " A"); var b = Book(term + " A"); var c = Book(term + " B");
        context.Books.AddRange(a, b, c); await context.SaveChangesAsync();
        var repository = new BookRepository(context);
        var first = await repository.GetPagedAsync(1, 2, term, null);
        var second = await repository.GetPagedAsync(2, 2, term, null);
        Assert.Equal(3, first.TotalCount); Assert.Equal(3, second.TotalCount);
        Assert.Equal(new[] {a.BookId, b.BookId}.OrderBy(x => x), first.Items.Select(x => x.BookId));
        Assert.Equal(c.BookId, Assert.Single(second.Items).BookId);
        Assert.Equal(3, first.Items.Concat(second.Items).Select(x => x.BookId).Distinct().Count());
        var extreme = await repository.GetPagedAsync(int.MaxValue, 100, term, null);
        Assert.Equal(3, extreme.TotalCount); Assert.Empty(extreme.Items);
    }

    private static Book Book(string title) => new() { Title = title, Author = "QA author", Barcode = Guid.NewGuid().ToString("N"),
        StockQuantity = 10, SellingPrice = 10000, IsActive = true, Category = new Category { Name = "QA " + Guid.NewGuid().ToString("N") } };
}
