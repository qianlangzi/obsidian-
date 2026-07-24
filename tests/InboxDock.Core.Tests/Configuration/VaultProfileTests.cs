using System.Text.Json;
using InboxDock.Core.Configuration;
using InboxDock.Core.Targets;

namespace InboxDock.Core.Tests.Configuration;

public sealed class VaultProfileTests
{
    [Fact]
    public void Validate_PassesForVaultWithDefaultTarget()
    {
        var targetId = Guid.NewGuid();
        var profile = new VaultProfile
        {
            Name = "我的知识库",
            VaultPath = "E:\\知识库",
            DefaultTargetId = targetId,
            CaptureTargets =
            [
                new CaptureTarget
                {
                    Id = targetId,
                    Name = "收件箱",
                    WriteMode = TargetWriteMode.CreateNote,
                    PathTemplate = "00 Inbox收件箱",
                },
            ],
        };

        var result = profile.Validate();

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_FailsWhenDefaultTargetIdPointsToMissingTarget()
    {
        var profile = new VaultProfile
        {
            Name = "我的知识库",
            VaultPath = "E:\\知识库",
            DefaultTargetId = Guid.NewGuid(),
            CaptureTargets = [],
        };

        var result = profile.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("默认目标", result.Message);
    }

    [Fact]
    public void Validate_FailsOnDuplicateTargetIds()
    {
        var id = Guid.NewGuid();
        var profile = new VaultProfile
        {
            Name = "我的知识库",
            VaultPath = "E:\\知识库",
            CaptureTargets =
            [
                new CaptureTarget { Id = id, Name = "收件箱", PathTemplate = "Inbox" },
                new CaptureTarget { Id = id, Name = "重复", PathTemplate = "Other" },
            ],
        };

        var result = profile.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("重复", result.Message);
    }

    [Fact]
    public void Validate_FailsWhenTargetNameIsEmpty()
    {
        var profile = new VaultProfile
        {
            Name = "我的知识库",
            VaultPath = "E:\\知识库",
            CaptureTargets =
            [
                new CaptureTarget { Name = string.Empty, PathTemplate = "Inbox" },
            ],
        };

        var result = profile.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("名称", result.Message);
    }

    [Fact]
    public void Validate_FailsWhenNonStagingTargetPathIsEmpty()
    {
        var profile = new VaultProfile
        {
            Name = "我的知识库",
            VaultPath = "E:\\知识库",
            CaptureTargets =
            [
                new CaptureTarget
                {
                    Name = "收件箱",
                    WriteMode = TargetWriteMode.AppendToFile,
                    PathTemplate = string.Empty,
                },
            ],
        };

        var result = profile.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("路径", result.Message);
    }

    [Fact]
    public void Validate_AllowsStagingOnlyTargetWithoutPath()
    {
        var profile = new VaultProfile
        {
            Name = "我的知识库",
            VaultPath = "E:\\知识库",
            CaptureTargets =
            [
                new CaptureTarget
                {
                    Name = "只暂存",
                    WriteMode = TargetWriteMode.StagingOnly,
                    PathTemplate = string.Empty,
                },
            ],
        };

        var result = profile.Validate();

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_FailsWhenVaultPathIsEmpty()
    {
        var profile = new VaultProfile
        {
            Name = "我的知识库",
            VaultPath = string.Empty,
        };

        var result = profile.Validate();

        Assert.False(result.IsValid);
    }

    [Fact]
    public void JsonRoundTrip_PreservesChinesePathsNamesAndTemplates()
    {
        var targetId = Guid.NewGuid();
        var profile = new VaultProfile
        {
            Id = Guid.NewGuid(),
            Name = "知识库",
            VaultPath = "E:\\知识库\\第一个仓库",
            DefaultTargetId = targetId,
            CaptureTargets =
            [
                new CaptureTarget
                {
                    Id = targetId,
                    Name = "今日日记",
                    WriteMode = TargetWriteMode.AppendToPeriodicFile,
                    PathTemplate = "01 Daily日常",
                    ContentTemplate = "## {{title}}\n\n{{content}}",
                },
            ],
            Theme = AppTheme.Dark,
            AutoHideDelay = TimeSpan.FromSeconds(30),
            GlobalHotkey = "Ctrl+Shift+Space",
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        var json = JsonSerializer.Serialize(profile, options);
        var deserialized = JsonSerializer.Deserialize<VaultProfile>(json, options);

        Assert.NotNull(deserialized);
        Assert.Equal(profile.Id, deserialized.Id);
        Assert.Equal(profile.Name, deserialized.Name);
        Assert.Equal(profile.VaultPath, deserialized.VaultPath);
        Assert.Equal(profile.DefaultTargetId, deserialized.DefaultTargetId);
        Assert.Equal(profile.Theme, deserialized.Theme);
        Assert.Equal(profile.AutoHideDelay, deserialized.AutoHideDelay);
        Assert.Equal(profile.GlobalHotkey, deserialized.GlobalHotkey);
        Assert.Single(deserialized.CaptureTargets);
        Assert.Equal("01 Daily日常", deserialized.CaptureTargets[0].PathTemplate);
        Assert.Equal("今日日记", deserialized.CaptureTargets[0].Name);
        Assert.Equal(TargetWriteMode.AppendToPeriodicFile, deserialized.CaptureTargets[0].WriteMode);
    }

    [Fact]
    public void Defaults_DoNotLeaveListsNull()
    {
        var profile = new VaultProfile();

        Assert.NotNull(profile.CaptureTargets);
        Assert.Empty(profile.CaptureTargets);
        Assert.NotNull(profile.DefaultAttachmentPolicy);
        Assert.Equal(TimeSpan.FromSeconds(5), profile.AutoHideDelay);
        Assert.Equal("Ctrl+Shift+Space", profile.GlobalHotkey);
    }
}
