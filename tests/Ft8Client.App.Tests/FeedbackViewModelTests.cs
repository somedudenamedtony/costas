// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using FluentAssertions;
using Ft8Client.App.Services;
using Ft8Client.App.ViewModels;
using Xunit;

namespace Ft8Client.App.Tests;

public class FeedbackViewModelTests
{
    [Fact]
    public void Build_Defaults_HasSystemLineButNoLog()
    {
        var logRead = false;
        var vm = new FeedbackViewModel(null, "tony@example.com", () => { logRead = true; return "log"; }, _ => true) { Message = "Hi" };

        var r = vm.Build();

        r.SystemInfo.Should().Contain("Costas");
        r.Log.Should().BeNull();
        logRead.Should().BeFalse("the log is only read when the operator ticks it");
    }

    [Fact]
    public void Build_LogTicked_IncludesLog()
    {
        var vm = new FeedbackViewModel(null, null, () => "the log", _ => true) { Message = "Hi", IncludeLog = true, IncludeSystem = false };

        var r = vm.Build();

        r.Log.Should().Be("the log");
        r.SystemInfo.Should().BeNull();
    }

    [Fact]
    public void EmailCommand_EmptyMessageOrBadAddress_CannotRun()
    {
        var vm = new FeedbackViewModel(null, "tony@example.com", () => null, _ => true);
        vm.EmailCommand.CanExecute(null).Should().BeFalse();
        vm.Message = "Hi";
        vm.EmailCommand.CanExecute(null).Should().BeTrue();
        vm.ReplyTo = "not an address";
        vm.EmailCommand.CanExecute(null).Should().BeFalse();
        vm.ReplyToError.Should().NotBeEmpty();
    }

    [Fact]
    public void Email_Opened_PassesMailtoLink()
    {
        string? opened = null;
        var vm = new FeedbackViewModel(null, "tony@example.com", () => null, l => { opened = l; return true; }) { Message = "Hi" };

        vm.EmailCommand.Execute(null);

        opened.Should().StartWith("mailto:tony@example.com?");
    }

    [Fact]
    public void NoRoute_NoClientOrEmail_True()
    {
        var vm = new FeedbackViewModel(null, null, () => null, _ => true);
        vm.NoRoute.Should().BeTrue();
        vm.SendCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Read_LongLogWithSecret_KeepsTailAndRemovesSecret()
    {
        var dir = Directory.CreateTempSubdirectory("costas-logtail-").FullName;
        try
        {
            File.WriteAllLines(Path.Combine(dir, "app-20261031.log"), Enumerable.Range(1, 500).Select(i => $"line {i} key=SECRET123"));
            var tail = LogTail.Read(dir, ["SECRET123"])!;
            tail.Should().NotContain("SECRET123").And.Contain("line 500").And.NotContain("line 300 ");
            tail.Split('\n').Length.Should().BeLessThanOrEqualTo(LogTail.MaxLines);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Read_NoFolder_Null() => LogTail.Read(Path.Combine(Path.GetTempPath(), "costas-no-such-folder-x"), []).Should().BeNull();
}
