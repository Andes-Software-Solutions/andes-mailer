using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Unit.Test.Repository.Sql.Interceptors;

public sealed class AuditTimestampInterceptorTests
{
    [Fact]
    public async Task SaveChangesAsync_AddedEntity_StampsDateCreated()
    {
        using var fixture = new SampleDbFixture();
        await using var ctx = fixture.CreateContext();
        var entity = new SampleEntity { Name = "first" };
        ctx.Add(entity);

        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(fixture.Time.GetUtcNow(), entity.DateCreated);
        Assert.Null(entity.DateModified);
    }

    [Fact]
    public void SaveChanges_AddedEntity_StampsDateCreated()
    {
        using var fixture = new SampleDbFixture();
        using var ctx = fixture.CreateContext();
        var entity = new SampleEntity { Name = "first" };
        ctx.Add(entity);

        ctx.SaveChanges();

        Assert.Equal(fixture.Time.GetUtcNow(), entity.DateCreated);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedEntity_StampsDateModifiedAndKeepsDateCreated()
    {
        using var fixture = new SampleDbFixture();
        await using var ctx = fixture.CreateContext();
        var entity = new SampleEntity { Name = "first" };
        ctx.Add(entity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var created = entity.DateCreated;
        fixture.Time.Advance(TimeSpan.FromMinutes(5));
        entity.Name = "second";

        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(created, entity.DateCreated);
        Assert.Equal(fixture.Time.GetUtcNow(), entity.DateModified);
    }

    [Fact]
    public async Task SaveChangesAsync_UnchangedEntity_LeavesDateModifiedNull()
    {
        using var fixture = new SampleDbFixture();
        await using var ctx = fixture.CreateContext();
        var entity = new SampleEntity { Name = "first" };
        ctx.Add(entity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Time.Advance(TimeSpan.FromMinutes(5));

        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(EntityState.Unchanged, ctx.Entry(entity).State);
        Assert.Null(entity.DateModified);
    }
}
