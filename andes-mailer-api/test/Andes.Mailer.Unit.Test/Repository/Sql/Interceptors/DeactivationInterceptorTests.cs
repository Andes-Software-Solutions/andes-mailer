using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Unit.Test.Repository.Sql.Interceptors;

public sealed class DeactivationInterceptorTests
{
    [Fact]
    public async Task SaveChangesAsync_RemovedEntity_KeepsRowAndStampsDeactivation()
    {
        using var fixture = new SampleDbFixture();
        var id = await SeedAsync(fixture);
        fixture.Time.Advance(TimeSpan.FromMinutes(5));

        await RemoveAsync(fixture, id);

        await using var ctx = fixture.CreateContext();
        var row = await ctx.Samples
            .IgnoreQueryFilters(["Deactivation"])
            .SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(fixture.Time.GetUtcNow(), row.DateDeactivated);
    }

    [Fact]
    public async Task SaveChangesAsync_RemovedEntity_StampsDateModified()
    {
        using var fixture = new SampleDbFixture();
        var id = await SeedAsync(fixture);
        fixture.Time.Advance(TimeSpan.FromMinutes(5));

        await RemoveAsync(fixture, id);

        await using var ctx = fixture.CreateContext();
        var row = await ctx.Samples
            .IgnoreQueryFilters(["Deactivation"])
            .SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(fixture.Time.GetUtcNow(), row.DateModified);
    }

    [Fact]
    public async Task SaveChangesAsync_RemovedEntity_HidesRowFromDefaultQueries()
    {
        using var fixture = new SampleDbFixture();
        var id = await SeedAsync(fixture);

        await RemoveAsync(fixture, id);

        await using var ctx = fixture.CreateContext();
        Assert.False(await ctx.Samples.AnyAsync(s => s.Id == id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveChangesAsync_RemovedStub_KeepsOtherColumns()
    {
        using var fixture = new SampleDbFixture();
        var id = await SeedAsync(fixture);
        var created = fixture.Time.GetUtcNow();
        fixture.Time.Advance(TimeSpan.FromMinutes(5));

        await using (var ctx = fixture.CreateContext())
        {
            var stub = new SampleEntity { Id = id, Name = string.Empty };
            ctx.Attach(stub);
            ctx.Remove(stub);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verifyCtx = fixture.CreateContext();
        var row = await verifyCtx.Samples
            .IgnoreQueryFilters(["Deactivation"])
            .SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal("first", row.Name);
        Assert.Equal(created, row.DateCreated);
        Assert.Equal(fixture.Time.GetUtcNow(), row.DateDeactivated);
    }

    #region Private methods

    private static async Task<Guid> SeedAsync(SampleDbFixture fixture)
    {
        await using var ctx = fixture.CreateContext();
        var entity = new SampleEntity { Name = "first" };
        ctx.Add(entity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return entity.Id;
    }

    private static async Task RemoveAsync(SampleDbFixture fixture, Guid id)
    {
        await using var ctx = fixture.CreateContext();
        var entity = await ctx.Samples.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
        ctx.Remove(entity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    #endregion
}
