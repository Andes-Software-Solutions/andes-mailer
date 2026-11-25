using Andes.Mailer.Api.Idempotency;
using Andes.Mailer.Api.Options;
using Andes.Mailer.Common.Enums;

using Microsoft.Extensions.Time.Testing;

namespace Andes.Mailer.Unit.Test.Api.Idempotency;

public sealed class IdempotencyStoreTests
{
    private const string Key = "caller:key";
    private const string Fingerprint = "fingerprint";

    private static readonly TimeSpan _expiration = TimeSpan.FromMinutes(5);

    private readonly FakeTimeProvider _time = new();
    private readonly IdempotencyStore _store;

    public IdempotencyStoreTests() =>
        _store = new IdempotencyStore(_time, Microsoft.Extensions.Options.Options.Create(new IdempotencyOptions { Expiration = _expiration }));

    [Fact]
    public void Begin_NewKey_Starts() =>
        Assert.Equal(IdempotencyOutcomes.Started, _store.Begin(Key, Fingerprint).Outcome);

    [Fact]
    public void Begin_SameKeyWhileRunning_IsInProgress()
    {
        _store.Begin(Key, Fingerprint);

        Assert.Equal(IdempotencyOutcomes.InProgress, _store.Begin(Key, Fingerprint).Outcome);
    }

    [Fact]
    public void Begin_SameKeyAfterSuccess_ReplaysResult()
    {
        var result = new object();
        var reservation = _store.Begin(Key, Fingerprint).Reservation;
        _store.Complete(Key, reservation, result);

        var lookup = _store.Begin(Key, Fingerprint);

        Assert.Equal(IdempotencyOutcomes.Completed, lookup.Outcome);
        Assert.Same(result, lookup.Result);
    }

    [Fact]
    public void Begin_SameKeyDifferentRequest_IsMismatch()
    {
        var reservation = _store.Begin(Key, Fingerprint).Reservation;
        _store.Complete(Key, reservation, new object());

        Assert.Equal(IdempotencyOutcomes.Mismatch, _store.Begin(Key, "other").Outcome);
    }

    [Fact]
    public void Begin_AfterRelease_StartsAgainWithAnyRequest()
    {
        var reservation = _store.Begin(Key, Fingerprint).Reservation;
        _store.Release(Key, reservation);

        Assert.Equal(IdempotencyOutcomes.Started, _store.Begin(Key, "fixed request").Outcome);
    }

    [Fact]
    public void Release_CompletedKey_KeepsResult()
    {
        var reservation = _store.Begin(Key, Fingerprint).Reservation;
        _store.Complete(Key, reservation, new object());

        _store.Release(Key, reservation);

        Assert.Equal(IdempotencyOutcomes.Completed, _store.Begin(Key, Fingerprint).Outcome);
    }

    [Fact]
    public void Begin_AfterExpiration_StartsAgain()
    {
        var reservation = _store.Begin(Key, Fingerprint).Reservation;
        _store.Complete(Key, reservation, new object());

        _time.Advance(_expiration);

        Assert.Equal(IdempotencyOutcomes.Started, _store.Begin(Key, Fingerprint).Outcome);
    }

    [Fact]
    public void Begin_JustBeforeExpiration_StillReplays()
    {
        var reservation = _store.Begin(Key, Fingerprint).Reservation;
        _store.Complete(Key, reservation, new object());

        _time.Advance(_expiration - TimeSpan.FromSeconds(1));

        Assert.Equal(IdempotencyOutcomes.Completed, _store.Begin(Key, Fingerprint).Outcome);
    }

    [Fact]
    public void Begin_RunningKeyPastExpiration_IsStillInProgress()
    {
        _store.Begin(Key, Fingerprint);

        _time.Advance(_expiration * 2);

        Assert.Equal(IdempotencyOutcomes.InProgress, _store.Begin(Key, Fingerprint).Outcome);
    }

    [Fact]
    public void Begin_RunningKeyReleasedAfterExpiration_StartsAgain()
    {
        var reservation = _store.Begin(Key, Fingerprint).Reservation;
        _time.Advance(_expiration * 2);

        _store.Release(Key, reservation);

        Assert.Equal(IdempotencyOutcomes.Started, _store.Begin(Key, Fingerprint).Outcome);
    }

    [Fact]
    public void Complete_AnotherReservation_LeavesTheRunningRequestInProgress()
    {
        var stale = _store.Begin(Key, Fingerprint).Reservation;
        _store.Release(Key, stale);
        _store.Begin(Key, Fingerprint);

        _store.Complete(Key, stale, new object());

        Assert.Equal(IdempotencyOutcomes.InProgress, _store.Begin(Key, Fingerprint).Outcome);
    }

    [Fact]
    public void Release_AnotherReservation_KeepsTheRunningRequestReserved()
    {
        var stale = _store.Begin(Key, Fingerprint).Reservation;
        _store.Release(Key, stale);
        _store.Begin(Key, Fingerprint);

        _store.Release(Key, stale);

        Assert.Equal(IdempotencyOutcomes.InProgress, _store.Begin(Key, Fingerprint).Outcome);
    }

    [Fact]
    public void Begin_DifferentKeys_AreIndependent()
    {
        _store.Begin(Key, Fingerprint);

        Assert.Equal(IdempotencyOutcomes.Started, _store.Begin("other:key", Fingerprint).Outcome);
    }
}
