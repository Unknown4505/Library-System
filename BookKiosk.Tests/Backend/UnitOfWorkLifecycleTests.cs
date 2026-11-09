using System.Reflection;
using BookKiosk.Infrastructure.Data;
using BookKiosk.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace BookKiosk.Tests.Backend;

public class UnitOfWorkLifecycleTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    public async Task Completion_clears_slot_disposes_and_preserves_primary_failure(bool commit, bool operationFails, bool disposeFails)
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().Options);
        using var work = new UnitOfWork(context);
        var slot = typeof(UnitOfWork).GetField("_transaction", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var transaction = new Mock<IDbContextTransaction>();
        var primary = new InvalidOperationException("primary failure");
        var cleanup = new InvalidOperationException("dispose failure");
        transaction.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(operationFails ? Task.FromException(primary) : Task.CompletedTask);
        transaction.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>()))
            .Returns(operationFails ? Task.FromException(primary) : Task.CompletedTask);
        transaction.Setup(x => x.DisposeAsync()).Returns(() =>
        {
            Assert.Null(slot.GetValue(work));
            return disposeFails ? ValueTask.FromException(cleanup) : ValueTask.CompletedTask;
        });
        slot.SetValue(work, transaction.Object);

        var error = await Record.ExceptionAsync(() => commit ? work.CommitAsync() : work.RollbackAsync());
        Assert.Same(operationFails ? primary : disposeFails ? cleanup : null, error);
        Assert.Null(slot.GetValue(work));
        transaction.Verify(x => x.DisposeAsync(), Times.Once);
        transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), commit ? Times.Once() : Times.Never());
        transaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), commit ? Times.Never() : Times.Once());

        // An empty slot is a no-op; a subsequent transaction can complete in the same scope.
        await work.CommitAsync();
        await work.RollbackAsync();
        var next = new Mock<IDbContextTransaction>();
        next.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        slot.SetValue(work, next.Object);
        await work.CommitAsync();
        next.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        next.Verify(x => x.DisposeAsync(), Times.Once);
        Assert.Null(slot.GetValue(work));
    }

    [Fact]
    public void Synchronous_dispose_failure_also_clears_slot()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().Options);
        var work = new UnitOfWork(context);
        var slot = typeof(UnitOfWork).GetField("_transaction", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var transaction = new Mock<IDbContextTransaction>();
        transaction.Setup(x => x.Dispose()).Throws(new InvalidOperationException("dispose failure"));
        slot.SetValue(work, transaction.Object);
        Assert.Throws<InvalidOperationException>(() => work.Dispose());
        Assert.Null(slot.GetValue(work));
        work.Dispose();
        transaction.Verify(x => x.Dispose(), Times.Once);
    }
}
