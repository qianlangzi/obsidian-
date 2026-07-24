using InboxDock.Core.Configuration;
using InboxDock.Core.Targets;
using InboxDock.Core.Vault;

namespace InboxDock.Core.Tests.Configuration;

public sealed class OnboardingSuggestionsTests
{
    [Fact]
    public void BuildDefaultTargets_AppendToFile_CreatesInboxTargetAsDefault()
    {
        var targets = OnboardingSuggestions.BuildDefaultTargets(TargetWriteMode.AppendToFile);

        var inbox = Assert.Single(targets);
        Assert.Equal("收件箱", inbox.Name);
        Assert.Equal(TargetWriteMode.AppendToFile, inbox.WriteMode);
        Assert.True(inbox.IsDefault);
    }

    [Fact]
    public void BuildDefaultTargets_CreateNote_CreatesCreateNoteTarget()
    {
        var targets = OnboardingSuggestions.BuildDefaultTargets(TargetWriteMode.CreateNote);

        var target = Assert.Single(targets);
        Assert.Equal(TargetWriteMode.CreateNote, target.WriteMode);
        Assert.True(target.IsDefault);
    }

    [Fact]
    public void BuildDefaultTargets_StagingOnly_CreatesStagingOnlyTarget()
    {
        var targets = OnboardingSuggestions.BuildDefaultTargets(TargetWriteMode.StagingOnly);

        var target = Assert.Single(targets);
        Assert.Equal(TargetWriteMode.StagingOnly, target.WriteMode);
        Assert.Empty(target.PathTemplate);
    }

    [Fact]
    public void BuildDefaultTargets_WithDailyNotes_AddsDailyTarget()
    {
        var discovery = new VaultDiscoveryResult
        {
            IsValid = true,
            DailyNotesFolder = "01 Daily日常",
            DailyNotesFormat = "YYYY-MM-DD",
            DailyNotesTemplate = "Templates/Daily.md",
        };

        var targets = OnboardingSuggestions.BuildDefaultTargets(TargetWriteMode.AppendToFile, discovery);

        Assert.Equal(2, targets.Count);
        var daily = targets.Single(t => t.Name == "今日日记");
        Assert.Equal(TargetWriteMode.AppendToPeriodicFile, daily.WriteMode);
        Assert.Equal("01 Daily日常", daily.PathTemplate);
        Assert.Contains("yyyy", daily.FileNameTemplate);
        Assert.False(daily.IsDefault);
    }

    [Fact]
    public void BuildDefaultTargets_WithObsidianAttachmentFolder_UsesFixedDirectory()
    {
        var discovery = new VaultDiscoveryResult
        {
            IsValid = true,
            AttachmentLocationMode = 1,
            AttachmentFolder = "05 Resources/Attachments",
        };

        var targets = OnboardingSuggestions.BuildDefaultTargets(TargetWriteMode.AppendToFile, discovery);

        var inbox = targets.Single(t => t.Name == "收件箱");
        Assert.Equal(AttachmentPolicyKind.FixedDirectory, inbox.AttachmentPolicy.Kind);
        Assert.Equal("05 Resources/Attachments", inbox.AttachmentPolicy.DirectoryTemplate);
    }

    [Fact]
    public void BuildDefaultTargets_WithoutDiscovery_UsesDatedAttachmentPolicy()
    {
        var targets = OnboardingSuggestions.BuildDefaultTargets(TargetWriteMode.CreateNote);

        var target = Assert.Single(targets);
        Assert.Equal(AttachmentPolicyKind.DatedDirectory, target.AttachmentPolicy.Kind);
    }

    [Fact]
    public void BuildDefaultTargets_DailyNotesWithoutFormat_UsesDefaultDateFormat()
    {
        var discovery = new VaultDiscoveryResult
        {
            IsValid = true,
            DailyNotesFolder = "Daily",
        };

        var targets = OnboardingSuggestions.BuildDefaultTargets(TargetWriteMode.StagingOnly, discovery);

        var daily = targets.Single(t => t.Name == "今日日记");
        Assert.Contains("yyyy-MM-dd", daily.FileNameTemplate);
    }
}
