using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class NotificationServiceTests
{
    private sealed class Scene
    {
        public required AppDbContext Db { get; init; }
        public required NotificationService Service { get; init; }
        public required User Marta { get; init; }
        public required User Luis { get; init; }
        public required User Pedro { get; init; }
        public required NotificationType Type { get; init; }
    }

    private static readonly Dictionary<string, string> Ana = new() { ["estudiante"] = "Ana" };

    private static Scene Create()
    {
        var db = TestDb.Create();
        var coord = TestData.AddRole(db, "Coord");

        return new Scene
        {
            Db = db,
            Service = new NotificationService(db),
            Marta = TestData.AddUser(db, "Marta", "Rojas", coord),
            Luis = TestData.AddUser(db, "Luis", "Gil", coord),
            Pedro = TestData.AddUser(db, "Pedro", "Cano"),
            Type = TestData.AddNotificationType(db, "T1", "{estudiante} faltó.", coord),
        };
    }

    private static async Task<int> QueueSavedAsync(Scene scene, NotificationTarget? target = null)
    {
        var notification = await scene.Service.QueueAsync("T1", Ana, target);
        await scene.Db.SaveChangesAsync();
        return notification!.NotificationId;
    }

    [Fact]
    public async Task Role_recipients_see_it_and_others_do_not()
    {
        var scene = Create();
        await QueueSavedAsync(scene);

        var forMarta = await scene.Service.ListAsync(scene.Marta.Id, null, null);

        Assert.Single(forMarta);
        Assert.Equal("Ana faltó.", forMarta[0].Message);
        Assert.Single(await scene.Service.ListAsync(scene.Luis.Id, null, null));
        Assert.Empty(await scene.Service.ListAsync(scene.Pedro.Id, null, null));
    }

    [Fact]
    public async Task A_direct_user_recipient_sees_it()
    {
        var scene = Create();
        var direct = TestData.AddNotificationType(scene.Db, "T2", "Aviso directo.");
        scene.Db.NotificationTypeRecipients.Add(new NotificationTypeRecipient
        {
            NotificationTypeId = direct.NotificationTypeId,
            UserId = scene.Pedro.Id,
        });
        await scene.Db.SaveChangesAsync();

        await scene.Service.QueueAsync("T2", Ana);
        await scene.Db.SaveChangesAsync();

        Assert.Single(await scene.Service.ListAsync(scene.Pedro.Id, null, null));
        Assert.Empty(await scene.Service.ListAsync(scene.Marta.Id, null, null));
    }

    [Fact]
    public async Task Read_is_per_user()
    {
        var scene = Create();
        var id = await QueueSavedAsync(scene);

        await scene.Service.MarkReadAsync(id, scene.Marta.Id);

        Assert.Equal(NotificationStates.Pending, (await scene.Service.ListAsync(scene.Marta.Id, null, null))[0].State);
        Assert.Equal(NotificationStates.Unread, (await scene.Service.ListAsync(scene.Luis.Id, null, null))[0].State);
        Assert.Equal(1, (await scene.Service.SummaryAsync(scene.Luis.Id)).Unread);
        Assert.Equal(0, (await scene.Service.SummaryAsync(scene.Marta.Id)).Unread);
    }

    [Fact]
    public async Task Done_is_shared_and_shows_who_did_it()
    {
        var scene = Create();
        var id = await QueueSavedAsync(scene);

        var done = await scene.Service.MarkDoneAsync([id], "Se llamó a la familia", scene.Marta.Id);
        var forLuis = (await scene.Service.ListAsync(scene.Luis.Id, null, null))[0];

        Assert.Equal(1, done);
        Assert.Equal(NotificationStates.Done, forLuis.State);
        Assert.Equal("Marta Rojas", forLuis.DoneBy);
        Assert.Equal("Se llamó a la familia", forLuis.DoneNote);
    }

    [Fact]
    public async Task A_second_done_does_not_overwrite_the_first()
    {
        var scene = Create();
        var id = await QueueSavedAsync(scene);
        await scene.Service.MarkDoneAsync([id], null, scene.Marta.Id);

        var second = await scene.Service.MarkDoneAsync([id], "otra", scene.Luis.Id);

        Assert.Equal(0, second);
        Assert.Equal("Marta Rojas", (await scene.Service.ListAsync(scene.Luis.Id, null, null))[0].DoneBy);
    }

    [Fact]
    public async Task Marking_an_invisible_notification_changes_nothing()
    {
        var scene = Create();
        var id = await QueueSavedAsync(scene);

        var done = await scene.Service.MarkDoneAsync([id], null, scene.Pedro.Id);
        await scene.Service.MarkReadAsync(id, scene.Pedro.Id);

        Assert.Equal(0, done);
        Assert.Equal(0, await scene.Db.NotificationReads.CountAsync());
        Assert.Equal(NotificationStatuses.Pending, (await scene.Db.Notifications.SingleAsync()).Status);
    }

    [Fact]
    public async Task Dedup_key_blocks_a_second_notification_after_saving()
    {
        var scene = Create();
        await QueueSavedAsync(scene, new NotificationTarget(DedupKey: "K1"));

        var second = await scene.Service.QueueAsync("T1", Ana, new NotificationTarget(DedupKey: "K1"));

        Assert.Null(second);
    }

    [Fact]
    public async Task Dedup_key_blocks_a_second_notification_before_saving()
    {
        var scene = Create();

        var first = await scene.Service.QueueAsync("T1", Ana, new NotificationTarget(DedupKey: "K2"));
        var second = await scene.Service.QueueAsync("T1", Ana, new NotificationTarget(DedupKey: "K2"));
        await scene.Db.SaveChangesAsync();

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Equal(1, await scene.Db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Inactive_or_unknown_type_queues_nothing()
    {
        var scene = Create();
        scene.Type.Active = false;
        await scene.Db.SaveChangesAsync();

        Assert.Null(await scene.Service.QueueAsync("T1", Ana));
        Assert.Null(await scene.Service.QueueAsync("NOPE", Ana));
    }

    [Fact]
    public async Task Empty_selection_is_rejected()
    {
        var scene = Create();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => scene.Service.MarkDoneAsync([], null, scene.Marta.Id));

        Assert.Equal("Seleccione al menos una notificación.", error.Message);
    }

    [Fact]
    public async Task HasPending_matches_type_student_and_year()
    {
        var scene = Create();
        var id = await QueueSavedAsync(scene, new NotificationTarget(StudentId: 7, YearId: 2027));

        Assert.True(await scene.Service.HasPendingAsync("T1", 7, 2027));
        Assert.False(await scene.Service.HasPendingAsync("T1", 7, 2026));

        await scene.Service.MarkDoneAsync([id], null, scene.Marta.Id);

        Assert.False(await scene.Service.HasPendingAsync("T1", 7, 2027));
    }

    [Fact]
    public async Task List_filters_by_state()
    {
        var scene = Create();
        var first = await QueueSavedAsync(scene);
        await QueueSavedAsync(scene);
        await scene.Service.MarkDoneAsync([first], null, scene.Marta.Id);

        Assert.Single(await scene.Service.ListAsync(scene.Luis.Id, "done", null));
        Assert.Single(await scene.Service.ListAsync(scene.Luis.Id, "UNREAD", null));
        Assert.Empty(await scene.Service.ListAsync(scene.Luis.Id, "PENDING", null));
    }

    [Fact]
    public async Task Mark_read_many_marks_only_visible_unread_for_that_user()
    {
        var scene = Create();
        var first = await QueueSavedAsync(scene);
        var second = await QueueSavedAsync(scene);
        var third = await QueueSavedAsync(scene);
        await scene.Service.MarkReadAsync(first, scene.Marta.Id);

        Assert.Equal(2, await scene.Service.MarkReadManyAsync([first, second, third, 9999], scene.Marta.Id));
        Assert.Equal(0, (await scene.Service.SummaryAsync(scene.Marta.Id)).Unread);
        Assert.Equal(3, (await scene.Service.SummaryAsync(scene.Luis.Id)).Unread);
        Assert.Equal(0, await scene.Service.MarkReadManyAsync([first, second], scene.Pedro.Id));
        Assert.Equal(0, await scene.Service.MarkReadManyAsync([first, second], scene.Marta.Id));
    }

    private static async Task<List<int>> QueueManyAsync(Scene scene, int count)
    {
        var queued = new List<Notification>();

        for (var i = 0; i < count; i++)
            queued.Add((await scene.Service.QueueAsync("T1", Ana))!);

        await scene.Db.SaveChangesAsync();
        return queued.Select(n => n.NotificationId).ToList();
    }

    [Fact]
    public async Task Page_shows_every_notification_with_counts()
    {
        var scene = Create();
        var ids = await QueueManyAsync(scene, 620);
        await scene.Service.MarkReadManyAsync(ids.Take(10).ToList(), scene.Marta.Id);
        await scene.Service.MarkDoneAsync(ids.Skip(10).Take(5).ToList(), null, scene.Luis.Id);

        var last = await scene.Service.PageAsync(scene.Marta.Id, null, null, 13, 50);

        Assert.Equal(620, last.Total);
        Assert.Equal(20, last.Rows.Count);
        Assert.Equal(13, last.Page);
        Assert.Equal(new NotificationCountsDto(620, 605, 10, 5), last.Counts);
        Assert.Equal(["T1"], last.Types.Select(t => t.Name));
        Assert.Equal(5, last.Rows.Count(row => row.State == NotificationStates.Done));
        Assert.Equal(NotificationStates.Done, last.Rows[^1].State);

        var unread = await scene.Service.PageAsync(scene.Marta.Id, "unread", null, 1, 50);

        Assert.Equal(605, unread.Total);
        Assert.Equal(50, unread.Rows.Count);
        Assert.All(unread.Rows, row => Assert.Equal(NotificationStates.Unread, row.State));
    }

    [Fact]
    public async Task Page_counts_and_total_match_each_state_filter()
    {
        var scene = Create();
        var other = TestData.AddNotificationType(scene.Db, "T2", "{estudiante} llegó tarde.", scene.Db.Roles.Single());
        var ids = await QueueManyAsync(scene, 6);
        await scene.Service.QueueAsync("T2", Ana);
        await scene.Db.SaveChangesAsync();
        await scene.Service.MarkReadManyAsync(ids.Take(2).ToList(), scene.Marta.Id);
        await scene.Service.MarkDoneAsync(ids.Skip(5).Take(1).ToList(), null, scene.Luis.Id);

        foreach (var typeId in new int?[] { null, scene.Type.NotificationTypeId, other.NotificationTypeId })
        {
            async Task<int> Count(string? state) => (await scene.Service.ListAsync(scene.Marta.Id, state, typeId)).Count;

            var expectedCounts = new NotificationCountsDto(await Count(null), await Count("unread"), await Count("pending"), await Count("done"));

            foreach (var state in new[] { null, "unread", "pending", "done", "otro" })
            {
                var page = await scene.Service.PageAsync(scene.Marta.Id, state, typeId, 1, 50);

                Assert.Equal(expectedCounts, page.Counts);
                Assert.Equal(await Count(state), page.Total);
            }
        }
    }

    [Fact]
    public async Task Page_clamps_page_and_size()
    {
        var scene = Create();
        await QueueManyAsync(scene, 3);

        var page = await scene.Service.PageAsync(scene.Marta.Id, null, null, 0, 1000);

        Assert.Equal(1, page.Page);
        Assert.Equal(NotificationService.MaxPageSize, page.PageSize);
        Assert.Equal(3, page.Rows.Count);

        var beyond = await scene.Service.PageAsync(scene.Marta.Id, null, null, 99, 50);

        Assert.Equal(1, beyond.Page);
        Assert.Equal(3, beyond.Rows.Count);
    }

    [Fact]
    public async Task Matching_actions_apply_to_the_whole_filter()
    {
        var scene = Create();
        var other = TestData.AddNotificationType(scene.Db, "T2", "{estudiante} llegó tarde.", scene.Db.Roles.Single());
        await QueueManyAsync(scene, 70);
        await scene.Service.QueueAsync("T2", Ana);
        await scene.Db.SaveChangesAsync();

        Assert.Equal(70, await scene.Service.MarkReadMatchingAsync(scene.Marta.Id, "unread", scene.Type.NotificationTypeId));
        Assert.Equal(1, (await scene.Service.SummaryAsync(scene.Marta.Id)).Unread);

        Assert.Equal(70, await scene.Service.MarkDoneMatchingAsync(scene.Marta.Id, "pending", scene.Type.NotificationTypeId, "Lote"));
        Assert.Equal(70, (await scene.Service.PageAsync(scene.Luis.Id, "done", null, 1, 50)).Total);
        Assert.Equal(1, (await scene.Service.PageAsync(scene.Luis.Id, null, other.NotificationTypeId, 1, 50)).Total);
        Assert.Equal(0, await scene.Service.MarkReadMatchingAsync(scene.Pedro.Id, null, null));
    }

    [Fact]
    public async Task Mark_read_many_rejects_an_empty_selection()
    {
        var scene = Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => scene.Service.MarkReadManyAsync([], scene.Marta.Id));
    }
}
