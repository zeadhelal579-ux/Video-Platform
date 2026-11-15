// File: ListVideosHandlerTests.cs
namespace VideoPlatform.Tests.Application;

using VideoPlatform.Application.Videos.Queries.ListVideos;
using VideoPlatform.Domain.Entities;
using VideoPlatform.Tests.TestSupport;
using Xunit;

public class ListVideosHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_returns_only_the_requesting_owners_videos_newest_first()
    {
        await using var db = TestAppDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();

        var older = Video.Create(Guid.NewGuid(), "Older", ownerId, "videos/1/original/a.mp4", Now);
        var newer = Video.Create(Guid.NewGuid(), "Newer", ownerId, "videos/2/original/b.mp4", Now.AddMinutes(5));
        var someoneElses = Video.Create(Guid.NewGuid(), "Not mine", otherOwnerId, "videos/3/original/c.mp4", Now);

        db.Videos.AddRange(older, newer, someoneElses);
        await db.SaveChangesAsync();

        var handler = new ListVideosHandler(db, new FakeStorageService());

        var result = await handler.Handle(new ListVideosQuery(ownerId));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal("Newer", result.Value.Items[0].Title);
        Assert.Equal("Older", result.Value.Items[1].Title);
    }

    [Fact]
    public async Task Handle_searches_titles_for_the_owner_before_paging()
    {
        await using var db = TestAppDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        db.Videos.AddRange(
            Video.Create(secondId, "فيديو clip second", ownerId, "second.mp4", Now),
            Video.Create(firstId, "فيديو CLIP first", ownerId, "first.mp4", Now),
            Video.Create(Guid.NewGuid(), "فيديو other title", ownerId, "other.mp4", Now.AddMinutes(1)),
            Video.Create(Guid.NewGuid(), "فيديو clip", Guid.NewGuid(), "private.mp4", Now.AddMinutes(1)));
        await db.SaveChangesAsync();
        var handler = new ListVideosHandler(db, new FakeStorageService());

        var query = new ListVideosQuery(ownerId, PageSize: 1, Q: "  فيديو CLIP  ");
        var first = await handler.Handle(query);
        var second = await handler.Handle(query with { Page = 2 });

        Assert.Equal(2, first.Value.TotalCount);
        Assert.Equal(firstId, Assert.Single(first.Value.Items).Id);
        Assert.Equal(secondId, Assert.Single(second.Value.Items).Id);
    }

    [Fact]
    public async Task Handle_returns_the_same_library_for_blank_search()
    {
        await using var db = TestAppDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        db.Videos.Add(Video.Create(Guid.NewGuid(), "My video", ownerId, "videos/1/original/a.mp4", Now));
        await db.SaveChangesAsync();
        var handler = new ListVideosHandler(db, new FakeStorageService());

        var result = await handler.Handle(new ListVideosQuery(ownerId, Q: " \t "));

        Assert.Equal("My video", Assert.Single(result.Value.Items).Title);
        Assert.Equal(1, result.Value.TotalCount);
    }

    [Fact]
    public async Task Handle_rejects_search_over_the_trimmed_limit()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = new ListVideosHandler(db, new FakeStorageService());

        var result = await handler.Handle(new ListVideosQuery(Guid.NewGuid(), Q: new string('a', 101)));

        Assert.True(result.IsFailure);
        Assert.Equal("Search must be at most 100 characters.", result.Error);
    }

    [Fact]
    public async Task Handle_rejects_an_offset_outside_the_supported_range()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = new ListVideosHandler(db, new FakeStorageService());

        var result = await handler.Handle(new ListVideosQuery(Guid.NewGuid(), Page: int.MaxValue, PageSize: 100));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_rejects_a_non_positive_page_number()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = new ListVideosHandler(db, new FakeStorageService());

        var result = await handler.Handle(new ListVideosQuery(Guid.NewGuid(), Page: 0));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_rejects_a_page_size_over_one_hundred()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = new ListVideosHandler(db, new FakeStorageService());

        var result = await handler.Handle(new ListVideosQuery(Guid.NewGuid(), Page: 1, PageSize: 101));

        Assert.True(result.IsFailure);
    }
}
