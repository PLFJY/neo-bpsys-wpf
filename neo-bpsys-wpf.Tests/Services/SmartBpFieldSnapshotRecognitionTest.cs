extern alias smartbp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using Xunit;
using SmartBpAutomaticParser = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpAutomaticParser;
using SmartBpRecognitionStateStore = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpRecognitionStateStore;
using AiStructuredOutputMode = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.AiStructuredOutputMode;
using SmartBpRecognitionPath = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionPath;
using SmartBpRecognitionSettings = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionSettings;
using SmartBpSnapshotFieldUpdate = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpSnapshotFieldUpdate;
using SmartBpSnapshotDeltaSlot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpSnapshotDeltaSlot;
using SmartBpBusinessStateRecognitionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpBusinessStateRecognitionResult;
using SmartBpRecognizedCharacterSlot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognizedCharacterSlot;
using SmartBpRecognizedPlayerCharacterSlot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognizedPlayerCharacterSlot;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 测试字段快照与仅阶段识别路径，这些路径用于以简单的字段快照识别替代旧版模型侧的 SnapshotDelta 机制。
/// </summary>
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
        Assert.Equal("未选择", update.Slots[2].CharacterName);
        Assert.Equal("unknown", update.Slots[3].SlotState);
        Assert.Null(update.PickedHun);
    }

    [Fact]
    public void ParseFieldSnapshot_PickedHun_ValidJson_ParsesCorrectly()
    {
        var raw = """{"field":"picked_hun","picked_hun":{"index":0,"slot_state":"selected","character_name":"厂长","player_id":"123"}}""";

        var update = SmartBpAutomaticParser.ParseFieldSnapshot(raw, "picked_hun", ["小说家"], ["厂长"]);

        Assert.Equal("picked_hun", update.Field);
        Assert.Null(update.Slots);
        Assert.NotNull(update.PickedHun);
        Assert.Equal(0, update.PickedHun!.Index);
        Assert.Equal("selected", update.PickedHun.SlotState);
        Assert.Equal("厂长", update.PickedHun.CharacterName);
        Assert.Equal("123", update.PickedHun.PlayerId);
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
        var raw = """{"field":"banned_sur","slots":[{"index":0,"slot_state":"invalid","character_name":"小说家"},{"index":1,"slot_state":"empty","character_name":"未选择"},{"index":2,"slot_state":"empty","character_name":"未选择"},{"index":3,"slot_state":"empty","character_name":"未选择"}]}""";

        Assert.Throws<InvalidDataException>(() =>
            SmartBpAutomaticParser.ParseFieldSnapshot(raw, "banned_sur", ["小说家"], ["厂长"]));
    }

    [Fact]
    public void ParseFieldSnapshot_SelectedSlotWithUnselectedCharacter_ThrowsInvalidData()
    {
        var raw = """{"field":"banned_sur","slots":[{"index":0,"slot_state":"selected","character_name":"未选择"},{"index":1,"slot_state":"empty","character_name":"未选择"},{"index":2,"slot_state":"empty","character_name":"未选择"},{"index":3,"slot_state":"empty","character_name":"未选择"}]}""";

        Assert.Throws<InvalidDataException>(() =>
            SmartBpAutomaticParser.ParseFieldSnapshot(raw, "banned_sur", ["小说家"], ["厂长"]));
    }

    [Fact]
    public void ParseFieldSnapshot_PickedHunWithSlots_ThrowsInvalidData()
    {
        var raw = """{"field":"picked_hun","slots":[{"index":0,"slot_state":"selected","character_name":"厂长"}],"picked_hun":{"index":0,"slot_state":"selected","character_name":"厂长"}}""";

        Assert.Throws<InvalidDataException>(() =>
            SmartBpAutomaticParser.ParseFieldSnapshot(raw, "picked_hun", ["小说家"], ["厂长"]));
    }

    [Fact]
    public void ApplyFieldSnapshot_SelectedSlot_UpdatesCharacterName()
    {
        var store = new SmartBpRecognitionStateStore();
        var update = new SmartBpSnapshotFieldUpdate
        {
            Field = "banned_sur",
            Slots =
            [
                new() { Index = 0, SlotState = "selected", CharacterName = "小说家" },
                new() { Index = 1, SlotState = "empty", CharacterName = "未选择" },
                new() { Index = 2, SlotState = "unknown", CharacterName = "未选择" },
                new() { Index = 3, SlotState = "unknown", CharacterName = "未选择" }
            ]
        };

        var diagnostics = store.ApplyFieldSnapshot("banned_sur", update, 1, DateTimeOffset.Now);
        var snapshot = store.Snapshot;

        Assert.Equal("小说家", snapshot.BannedSur[0].CharacterName);
        Assert.Equal("未选择", snapshot.BannedSur[1].CharacterName);
        Assert.Contains(diagnostics, d => d.Contains("Applied banned_sur[0]") && d.Contains("小说家"));
        Assert.Contains(diagnostics, d => d.Contains("Cleared banned_sur[1]"));
        Assert.Contains(diagnostics, d => d.Contains("Preserved banned_sur[2]"));
    }

    [Fact]
    public void ApplyFieldSnapshot_UnknownSlot_PreservesPreviousValue()
    {
        var store = new SmartBpRecognitionStateStore();
        var firstUpdate = new SmartBpSnapshotFieldUpdate
        {
            Field = "banned_sur",
            Slots =
            [
                new() { Index = 0, SlotState = "selected", CharacterName = "小说家" },
                new() { Index = 1, SlotState = "empty", CharacterName = "未选择" },
                new() { Index = 2, SlotState = "empty", CharacterName = "未选择" },
                new() { Index = 3, SlotState = "empty", CharacterName = "未选择" }
            ]
        };
        store.ApplyFieldSnapshot("banned_sur", firstUpdate, 1, DateTimeOffset.Now);

        var secondUpdate = new SmartBpSnapshotFieldUpdate
        {
            Field = "banned_sur",
            Slots =
            [
                new() { Index = 0, SlotState = "unknown", CharacterName = "未选择" },
                new() { Index = 1, SlotState = "empty", CharacterName = "未选择" },
                new() { Index = 2, SlotState = "empty", CharacterName = "未选择" },
                new() { Index = 3, SlotState = "empty", CharacterName = "未选择" }
            ]
        };
        store.ApplyFieldSnapshot("banned_sur", secondUpdate, 2, DateTimeOffset.Now);
        var snapshot = store.Snapshot;

        Assert.Equal("小说家", snapshot.BannedSur[0].CharacterName);
    }

    [Fact]
    public void ApplyFieldSnapshot_PickedHun_UpdatesHunterSlot()
    {
        var store = new SmartBpRecognitionStateStore();
        var update = new SmartBpSnapshotFieldUpdate
        {
            Field = "picked_hun",
            PickedHun = new() { Index = 0, SlotState = "selected", CharacterName = "厂长", PlayerId = "42" }
        };

        store.ApplyFieldSnapshot("picked_hun", update, 1, DateTimeOffset.Now);
        var snapshot = store.Snapshot;

        Assert.Equal("厂长", snapshot.PickedHun.CharacterName);
        Assert.Equal("42", snapshot.PickedHun.PlayerId);
    }

    [Fact]
    public void ApplyFieldSnapshot_StaleFrameSequence_IsIgnored()
    {
        var store = new SmartBpRecognitionStateStore();
        var newerUpdate = new SmartBpSnapshotFieldUpdate
        {
            Field = "banned_sur",
            Slots =
            [
                new() { Index = 0, SlotState = "selected", CharacterName = "小说家" },
                new() { Index = 1, SlotState = "empty", CharacterName = "未选择" },
                new() { Index = 2, SlotState = "empty", CharacterName = "未选择" },
                new() { Index = 3, SlotState = "empty", CharacterName = "未选择" }
            ]
        };
        store.ApplyFieldSnapshot("banned_sur", newerUpdate, 5, DateTimeOffset.Now);

        var staleUpdate = new SmartBpSnapshotFieldUpdate
        {
            Field = "banned_sur",
            Slots =
            [
                new() { Index = 0, SlotState = "selected", CharacterName = "昆虫学者" },
                new() { Index = 1, SlotState = "empty", CharacterName = "未选择" },
                new() { Index = 2, SlotState = "empty", CharacterName = "未选择" },
                new() { Index = 3, SlotState = "empty", CharacterName = "未选择" }
            ]
        };
        var diagnostics = store.ApplyFieldSnapshot("banned_sur", staleUpdate, 3, DateTimeOffset.Now);
        var snapshot = store.Snapshot;

        Assert.Equal("小说家", snapshot.BannedSur[0].CharacterName);
        Assert.Contains(diagnostics, d => d.Contains("Ignored stale field snapshot"));
    }

    [Fact]
    public void ApplyPhase_UpdatesPhaseWithoutTouchingFields()
    {
        var store = new SmartBpRecognitionStateStore();
        var fieldUpdate = new SmartBpSnapshotFieldUpdate
        {
            Field = "banned_sur",
            Slots =
            [
                new() { Index = 0, SlotState = "selected", CharacterName = "小说家" },
                new() { Index = 1, SlotState = "empty", CharacterName = "未选择" },
                new() { Index = 2, SlotState = "empty", CharacterName = "未选择" },
                new() { Index = 3, SlotState = "empty", CharacterName = "未选择" }
            ]
        };
        store.ApplyFieldSnapshot("banned_sur", fieldUpdate, 1, DateTimeOffset.Now);

        store.ApplyPhase("选择监管者", 2);
        var snapshot = store.Snapshot;

        Assert.Equal("选择监管者", snapshot.Phase);
        Assert.Equal("小说家", snapshot.BannedSur[0].CharacterName);
    }

    [Fact]
    public void Settings_Defaults_UseLegacySnapshotDeltaRecognitionIsFalse()
    {
        var settings = new SmartBpRecognitionSettings();

        Assert.False(settings.UseLegacySnapshotDeltaRecognition);
    }

    [Fact]
    public void RecognitionPath_Enum_HasFourValues()
    {
        Assert.True(Enum.IsDefined(typeof(SmartBpRecognitionPath), "PhaseOnly"));
        Assert.True(Enum.IsDefined(typeof(SmartBpRecognitionPath), "FieldSnapshot"));
        Assert.True(Enum.IsDefined(typeof(SmartBpRecognitionPath), "FullFieldSnapshot"));
        Assert.True(Enum.IsDefined(typeof(SmartBpRecognitionPath), "LegacyDelta"));
    }

    [Fact]
    public void AiStructuredOutputMode_Enum_HasTwoValues()
    {
        Assert.True(Enum.IsDefined(typeof(AiStructuredOutputMode), "JsonSchemaStrict"));
        Assert.True(Enum.IsDefined(typeof(AiStructuredOutputMode), "JsonPromptAndRepair"));
    }
}
