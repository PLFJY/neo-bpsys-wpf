extern alias smartbp;

using System;
using System.IO;
using System.Linq;
using neo_bpsys_wpf.Core.Models;
using Xunit;
using SmartBpAutomaticParser = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpAutomaticParser;
using SmartBpRecognitionPath = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionPath;
using SmartBpRecognitionSettings = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionSettings;
using SmartBpSnapshotRecognitionPlanner = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpSnapshotRecognitionPlanner;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class SmartBpFieldSnapshotRecognitionTest
{
    [Fact]
    public void ParseFieldSnapshot_BannedSur_ValidJson_ParsesCorrectly()
    {
        var raw = """{"field":"banned_sur","slots":[{"index":0,"slot_state":"selected","character_name":"小说家"},{"index":1,"slot_state":"selected","character_name":"昆虫学者"},{"index":2,"slot_state":"empty","character_name":"未选择"},{"index":3,"slot_state":"unknown","character_name":"未选择"}]}""";

        var update = SmartBpAutomaticParser.ParseFieldSnapshot(raw, "banned_sur", ["小说家", "昆虫学者"], ["厂长"]);

        Assert.Equal("banned_sur", update.Field);
        Assert.NotNull(update.Slots);
        Assert.Equal(4, update.Slots!.Count);
        Assert.Equal("selected", update.Slots[0].SlotState);
        Assert.Equal("小说家", update.Slots[0].CharacterName);
        Assert.Equal("empty", update.Slots[2].SlotState);
        Assert.Equal("unknown", update.Slots[3].SlotState);
        Assert.Null(update.PickedHun);
    }

    [Fact]
    public void ParseFieldSnapshot_PickedHun_ValidJson_ParsesCorrectly()
    {
        var raw = """{"field":"picked_hun","picked_hun":{"index":0,"slot_state":"selected","character_name":"厂长","player_id":"123"}}""";

        var update = SmartBpAutomaticParser.ParseFieldSnapshot(raw, "picked_hun", ["小说家"], ["厂长"]);

        Assert.Null(update.Slots);
        Assert.Equal("厂长", update.PickedHun?.CharacterName);
        Assert.Equal("123", update.PickedHun?.PlayerId);
    }

    [Fact]
    public void ParseFieldSnapshot_FieldMismatch_ThrowsInvalidData()
    {
        var raw = """{"field":"banned_hun","slots":[]}""";

        Assert.Throws<InvalidDataException>(() =>
            SmartBpAutomaticParser.ParseFieldSnapshot(raw, "banned_sur", ["小说家"], ["厂长"]));
    }

    [Fact]
    public void ParseFieldSnapshot_WrongSlotCount_ThrowsInvalidData()
    {
        var raw = """{"field":"banned_sur","slots":[{"index":0,"slot_state":"selected","character_name":"小说家"}]}""";

        Assert.Throws<InvalidDataException>(() =>
            SmartBpAutomaticParser.ParseFieldSnapshot(raw, "banned_sur", ["小说家"], ["厂长"]));
    }

    [Fact]
    public void ParseFieldSnapshot_InvalidSlotState_ThrowsInvalidData()
    {
        var raw = """{"field":"banned_hun","slots":[{"index":0,"slot_state":"waiting","character_name":"未选择"},{"index":1,"slot_state":"unknown","character_name":"未选择"}]}""";

        Assert.Throws<InvalidDataException>(() =>
            SmartBpAutomaticParser.ParseFieldSnapshot(raw, "banned_hun", ["小说家"], ["厂长"]));
    }

    [Fact]
    public void ParseFieldSnapshot_SelectedSlotWithUnselectedCharacter_ThrowsInvalidData()
    {
        var raw = """{"field":"picked_sur","slots":[{"index":0,"slot_state":"selected","character_name":"未选择"},{"index":1,"slot_state":"unknown","character_name":"未选择"},{"index":2,"slot_state":"unknown","character_name":"未选择"},{"index":3,"slot_state":"unknown","character_name":"未选择"}]}""";

        Assert.Throws<InvalidDataException>(() =>
            SmartBpAutomaticParser.ParseFieldSnapshot(raw, "picked_sur", ["小说家"], ["厂长"]));
    }

    [Fact]
    public void ParseFieldSnapshot_PickedHunWithSlots_ThrowsInvalidData()
    {
        var raw = """{"field":"picked_hun","slots":[],"picked_hun":{"index":0,"slot_state":"selected","character_name":"厂长"}}""";

        Assert.Throws<InvalidDataException>(() =>
            SmartBpAutomaticParser.ParseFieldSnapshot(raw, "picked_hun", ["小说家"], ["厂长"]));
    }

    [Fact]
    public void PlannerAlwaysRequestsAllCurrentFrameRoleFields()
    {
        var planner = new SmartBpSnapshotRecognitionPlanner();
        var request = planner.BuildRequest(new GameGuidanceRuntimeSnapshot(false, -1, null, [], null, []));

        Assert.Equal(
            ["banned_sur", "banned_hun", "picked_sur", "picked_hun"],
            request.RequestedFields);
    }

    [Fact]
    public void PlannerDoesNotDependOnPreviousFramePhaseOrSmartBpState()
    {
        var planner = new SmartBpSnapshotRecognitionPlanner();
        var beforeStart = planner.BuildRequest(new GameGuidanceRuntimeSnapshot(false, -1, null, [], null, []));
        var active = planner.BuildRequest(new GameGuidanceRuntimeSnapshot(
            true,
            7,
            neo_bpsys_wpf.Core.Enums.GameAction.PickSur,
            [2],
            null,
            []));

        Assert.Equal(beforeStart.RequestedFields, active.RequestedFields);
        Assert.Equal(4, active.RequestedRegions.Count);
    }

    [Fact]
    public void Settings_Defaults_UseLegacySnapshotDeltaRecognitionIsFalse()
    {
        Assert.False(new SmartBpRecognitionSettings().UseLegacySnapshotDeltaRecognition);
    }

    [Fact]
    public void RecognitionPath_Enum_HasFourValues()
    {
        Assert.Equal(4, Enum.GetValues<SmartBpRecognitionPath>().Length);
    }
}
