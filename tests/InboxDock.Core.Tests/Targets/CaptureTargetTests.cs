using System.Text.Json;
using InboxDock.Core.Configuration;
using InboxDock.Core.Targets;

namespace InboxDock.Core.Tests.Targets;

public sealed class CaptureTargetTests
{
    [Fact]
    public void Defaults_AreSafeForDeserialization()
    {
        var target = new CaptureTarget();

        Assert.NotEqual(Guid.Empty, target.Id);
        Assert.Equal(string.Empty, target.Name);
        Assert.Equal(TargetWriteMode.CreateNote, target.WriteMode);
        Assert.Equal(AttachmentPolicyKind.DatedDirectory, target.AttachmentPolicy.Kind);
        Assert.Equal(PostCaptureBehavior.RemoveStaged, target.PostCaptureBehavior);
        Assert.Equal(1, target.Revision);
    }

    [Fact]
    public void WithNewIdentity_GeneratesFreshIdAndResetsRevision()
    {
        var target = new CaptureTarget
        {
            Id = Guid.NewGuid(),
            Name = "收件箱",
            Revision = 5,
        };

        var copy = target.WithNewIdentity();

        Assert.NotEqual(target.Id, copy.Id);
        Assert.NotEqual(Guid.Empty, copy.Id);
        Assert.Equal("收件箱", copy.Name);
        Assert.Equal(1, copy.Revision);
    }

    [Fact]
    public void WriteModes_CoverTheFourSupportedKinds()
    {
        var modes = Enum.GetValues<TargetWriteMode>();
        Assert.Equal(4, modes.Length);
        Assert.Contains(TargetWriteMode.AppendToFile, modes);
        Assert.Contains(TargetWriteMode.AppendToPeriodicFile, modes);
        Assert.Contains(TargetWriteMode.CreateNote, modes);
        Assert.Contains(TargetWriteMode.StagingOnly, modes);
    }

    [Fact]
    public void AttachmentPolicyKinds_CoverTheFiveSupportedStrategies()
    {
        var kinds = Enum.GetValues<AttachmentPolicyKind>();
        Assert.Equal(5, kinds.Length);
        Assert.Contains(AttachmentPolicyKind.FollowObsidian, kinds);
        Assert.Contains(AttachmentPolicyKind.FixedDirectory, kinds);
        Assert.Contains(AttachmentPolicyKind.DatedDirectory, kinds);
        Assert.Contains(AttachmentPolicyKind.BesideNote, kinds);
        Assert.Contains(AttachmentPolicyKind.StagingOnly, kinds);
    }

    [Fact]
    public void CaptureTarget_JsonRoundTripsWithChinese()
    {
        var target = new CaptureTarget
        {
            Id = Guid.Parse("12345678-1234-1234-1234-1234567890ab"),
            Name = "今日日记",
            WriteMode = TargetWriteMode.AppendToPeriodicFile,
            PathTemplate = "01 Daily日常",
            FileNameTemplate = "{{date:yyyy-MM-dd}}",
            ContentTemplate = "## {{title}}\n\n{{content}}",
            HeadingName = "今日记录",
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        var json = JsonSerializer.Serialize(target, options);
        var deserialized = JsonSerializer.Deserialize<CaptureTarget>(json, options);

        Assert.NotNull(deserialized);
        Assert.Equal(target, deserialized);
    }
}
