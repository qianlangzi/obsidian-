using InboxDock.Core.Staging;
using InboxDock.Core.Tests.Support;

namespace InboxDock.Core.Tests.Staging;

public sealed class StagingStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsCardsAndUnicodeDraft()
    {
        using var root = new TemporaryDirectory();
        var store = new StagingStore(root.Path);
        var createdAt = new DateTimeOffset(2026, 7, 14, 9, 30, 0, TimeSpan.FromHours(8));
        var stagedPath = Path.Combine(root.Path, "files", "id", "报告.pdf");
        var items = new[]
        {
            new StagedMaterial(Guid.NewGuid(), StagedMaterialKind.Text, "随手记", createdAt,
                StagedMaterialStatus.Deferred, [], "一段文字"),
            new StagedMaterial(Guid.NewGuid(), StagedMaterialKind.Link, "example.com", createdAt,
                StagedMaterialStatus.AwaitingConfirmation, [], "https://example.com/a?b=1"),
            new StagedMaterial(Guid.NewGuid(), StagedMaterialKind.Files, "报告.pdf", createdAt,
                StagedMaterialStatus.Failed,
                [new StagedFile("D:\\来源\\报告.pdf", "报告.pdf", stagedPath, 42)],
                LastError: "等待重试",
                Note: "先阅读第二章"),
        };
        var snapshot = new StagingSnapshot(items, "今天想到：材料桶");

        await store.SaveAsync(snapshot);
        var result = await new StagingStore(root.Path).LoadAsync();

        Assert.Null(result.Error);
        Assert.Equal(snapshot.DraftText, result.Snapshot.DraftText);
        Assert.Equal(items.Length, result.Snapshot.Items.Count);
        for (var index = 0; index < items.Length; index++)
        {
            var expected = items[index];
            var actual = result.Snapshot.Items[index];
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Kind, actual.Kind);
            Assert.Equal(expected.Title, actual.Title);
            Assert.Equal(expected.CreatedAt, actual.CreatedAt);
            Assert.Equal(expected.Status, actual.Status);
            Assert.Equal(expected.Content, actual.Content);
            Assert.Equal(expected.LastError, actual.LastError);
            Assert.Equal(expected.Note, actual.Note);
            Assert.Equal(expected.Files.ToArray(), actual.Files.ToArray());
        }
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsEmptySnapshot()
    {
        using var root = new TemporaryDirectory();

        var result = await new StagingStore(root.Path).LoadAsync();

        Assert.Null(result.Error);
        Assert.Empty(result.Snapshot.Items);
        Assert.Equal(string.Empty, result.Snapshot.DraftText);
    }

    [Fact]
    public async Task LoadAsync_WhenJsonIsInvalid_ReturnsErrorWithoutChangingSource()
    {
        using var root = new TemporaryDirectory();
        var store = new StagingStore(root.Path);
        Directory.CreateDirectory(root.Path);
        var bytes = "{这不是 JSON"u8.ToArray();
        await File.WriteAllBytesAsync(store.DataPath, bytes);

        var result = await store.LoadAsync();

        Assert.NotNull(result.Error);
        Assert.Empty(result.Snapshot.Items);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(store.DataPath));
    }

    [Fact]
    public async Task LoadAsync_OldJsonWithoutPreferredTargetId_LoadsWithNullTarget()
    {
        using var root = new TemporaryDirectory();
        var store = new StagingStore(root.Path);
        Directory.CreateDirectory(root.Path);
        // 模拟 v0.2 旧版暂存 JSON：缺少 PreferredTargetId 字段。
        var oldJson = """
            {
              "Items": [
                {
                  "Id": "00000000-0000-0000-0000-000000000001",
                  "Kind": "Text",
                  "Title": "旧版材料",
                  "CreatedAt": "2026-07-01T08:00:00+08:00",
                  "Status": "AwaitingConfirmation",
                  "Files": [],
                  "Content": "旧版内容",
                  "LastError": null,
                  "Note": null
                }
              ],
              "DraftText": ""
            }
            """;
        await File.WriteAllTextAsync(store.DataPath, oldJson);

        var result = await store.LoadAsync();

        Assert.Null(result.Error);
        var material = Assert.Single(result.Snapshot.Items);
        Assert.Equal("旧版材料", material.Title);
        Assert.Null(material.PreferredTargetId);
    }

    [Fact]
    public async Task SaveAndLoadAsync_PreservesPreferredTargetId()
    {
        using var root = new TemporaryDirectory();
        var store = new StagingStore(root.Path);
        var targetId = Guid.NewGuid();
        var snapshot = new StagingSnapshot(
            [
                new StagedMaterial(
                    Guid.NewGuid(),
                    StagedMaterialKind.Text,
                    "带目标",
                    DateTimeOffset.Now,
                    StagedMaterialStatus.AwaitingConfirmation,
                    [],
                    "内容",
                    PreferredTargetId: targetId),
            ],
            string.Empty);

        await store.SaveAsync(snapshot);
        var result = await new StagingStore(root.Path).LoadAsync();

        Assert.Null(result.Error);
        var material = Assert.Single(result.Snapshot.Items);
        Assert.Equal(targetId, material.PreferredTargetId);
    }
}
