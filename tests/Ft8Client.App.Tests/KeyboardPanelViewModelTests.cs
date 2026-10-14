// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Reflection;
using FluentAssertions;
using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Keyboard;

namespace Ft8Client.App.Tests;

public class KeyboardPanelViewModelTests
{
    public class Recorder : DispatchProxy
    {
        public List<(string Typed, bool OnAir)> Enters { get; } = [];

        public List<char> Typed { get; } = [];

        public string? EnterResult { get; set; }

        public bool TypeAccepts { get; set; } = true;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IOperateCommands.KeyboardEnter):
                    Enters.Add(((string)args![0]!, (bool)args[1]!));
                    return EnterResult;
                case nameof(IOperateCommands.KeyboardType):
                    if (!TypeAccepts) return false;
                    Typed.Add((char)args![0]!);
                    return true;
                default:
                    return null;
            }
        }
    }

    private static (KeyboardPanelViewModel Panel, Recorder Commands) Build(bool rtty = false)
    {
        var commands = DispatchProxy.Create<IOperateCommands, Recorder>();
        var panel = new KeyboardPanelViewModel(commands);
        panel.Apply(new SessionSnapshot { Mode = rtty ? Mode.Rtty : Mode.Psk31 });
        return (panel, (Recorder)(object)commands);
    }

    [Fact]
    public void Enter_Refused_KeepsTypedLine()
    {
        var (panel, commands) = Build(rtty: true);
        panel.Typed = "HELLO @ TEST";
        commands.EnterResult = "Not sent: this mode cannot send '@'. Remove it and send again.";

        panel.Enter();

        panel.Typed.Should().Be("HELLO @ TEST");
    }

    [Fact]
    public void Enter_Sent_ClearsTypedLine()
    {
        var (panel, commands) = Build();
        panel.Typed = "hello";

        panel.Enter();

        commands.Enters.Should().Equal(("hello", false));
        panel.Typed.Should().BeEmpty();
    }

    [Fact]
    public void Enter_OnAir_SendsNoTextAndSaysOnAir()
    {
        var (panel, commands) = Build();
        panel.OnAir = true;

        panel.Enter();

        commands.Enters.Should().Equal((string.Empty, true));
    }

    [Fact]
    public void TypeOnAir_RttyUnsendableKey_RefusedAndNamed()
    {
        var (panel, commands) = Build(rtty: true);
        panel.OnAir = true;

        panel.TypeOnAir("a@b");

        commands.Typed.Should().Equal('a', 'b');
        panel.HasKeyNote.Should().BeTrue();
        panel.KeyNote.Should().Contain("'@'");
        panel.Typed.Should().BeEmpty();
    }

    [Fact]
    public void TypeOnAir_OverEnds_ClearsRefusal()
    {
        var (panel, _) = Build(rtty: true);
        panel.OnAir = true;
        panel.TypeOnAir("@");

        panel.OnAir = false;

        panel.HasKeyNote.Should().BeFalse();
    }

    [Fact]
    public void TypeOnAir_OverClosing_KeysKeptInTypedLine()
    {
        var (panel, commands) = Build(rtty: true);
        panel.OnAir = true;
        commands.TypeAccepts = false;

        panel.TypeOnAir("cq");

        panel.Typed.Should().Be("CQ");
        panel.HasKeyNote.Should().BeFalse();
    }

    [Fact]
    public void Typed_RttyUnsendableCharacters_Flagged()
    {
        var (panel, _) = Build(rtty: true);

        panel.Typed = "HELLO @ 50% é";

        panel.HasKeyNote.Should().BeTrue();
        panel.KeyNote.Should().Contain("'@'").And.Contain("'%'").And.Contain("'é'");
        panel.Typed = "HELLO";
        panel.HasKeyNote.Should().BeFalse();
    }

    [Fact]
    public void Typed_PskAscii_NotFlagged()
    {
        var (panel, _) = Build();

        panel.Typed = "hello @ 50% <>";

        panel.HasKeyNote.Should().BeFalse();
    }

    [Fact]
    public void TypeAnywhere_Receiving_AddsToLineInRttyCapitals()
    {
        var (panel, commands) = Build(rtty: true);
        panel.Typed = "CQ";

        panel.TypeAnywhere(" de");

        panel.Typed.Should().Be("CQ DE");
        commands.Typed.Should().BeEmpty();
    }

    [Fact]
    public void TypeAnywhere_OnAir_GoesToTransmitter()
    {
        var (panel, commands) = Build();
        panel.OnAir = true;

        panel.TypeAnywhere("hi");

        commands.Typed.Should().Equal('h', 'i');
        panel.Typed.Should().BeEmpty();
    }

    [Fact]
    public void BackspaceAnywhere_Receiving_RemovesLastCharacter()
    {
        var (panel, _) = Build();
        panel.Typed = "hello";

        panel.BackspaceAnywhere();

        panel.Typed.Should().Be("hell");
    }

    [Fact]
    public void Apply_SixMacros_HintNamesAllKeys()
    {
        var (panel, _) = Build();
        var macros = Macro.Defaults.Append(new Macro(6, "QSL", "QSL VIA {MYCALL}")).ToList();

        panel.Apply(new SessionSnapshot { Mode = Mode.Psk31, Macros = macros });

        panel.Hints.Should().Contain("F1 to F6 send a macro");
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3, 4, 5 }, "F1 to F5")]
    [InlineData(new[] { 1, 2, 4 }, "F1, F2 and F4")]
    [InlineData(new[] { 1, 2 }, "F1 and F2")]
    [InlineData(new[] { 3 }, "F3")]
    [InlineData(new int[0], "")]
    public void KeysText_Keys_NamesEach(int[] keys, string expected) =>
        KeyboardPanelViewModel.KeysText(keys).Should().Be(expected);

    [Fact]
    public void Apply_NewOver_KeepsRowsAlreadyShown()
    {
        var (panel, _) = Build();
        var t = new DateTime(2026, 10, 13, 18, 0, 0, DateTimeKind.Utc);
        var first = new ConversationLine(t, "KP4JRS", "CQ CQ DE KP4JRS K", false);
        panel.Apply(new SessionSnapshot { Mode = Mode.Psk31, Conversation = [first] });
        var row = panel.Conversation[0];

        panel.Apply(new SessionSnapshot { Mode = Mode.Psk31, Conversation = [first, new ConversationLine(t.AddSeconds(20), "W7LIT", "KP4JRS DE W7LIT K", true)] });

        panel.Conversation.Should().HaveCount(2);
        panel.Conversation[0].Should().BeSameAs(row);
    }
}
