// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Services;

namespace Ft8Client.App.Tests;

public class WorkQueueTests
{
    [Fact]
    public void Post_SeveralItems_RunInOrderOffTheCallersThread()
    {
        var ran = new List<(int Item, int Thread)>();
        using var q = new WorkQueue("TestWork", _ => { });
        for (var i = 0; i < 50; i++)
        {
            var item = i;
            q.Post(() => ran.Add((item, Environment.CurrentManagedThreadId)));
        }
        q.Flush(TimeSpan.FromSeconds(5)).Should().BeTrue();

        ran.Select(x => x.Item).Should().Equal(Enumerable.Range(0, 50));
        ran.Select(x => x.Thread).Distinct().Should().ContainSingle().Which.Should().NotBe(Environment.CurrentManagedThreadId);
    }

    [Fact]
    public void Post_WorkThrows_ErrorReportedAndLaterWorkStillRuns()
    {
        var errors = new List<Exception>();
        var ran = false;
        using var q = new WorkQueue("TestWork", errors.Add);
        q.Post(() => throw new IOException("disk full"));
        q.Post(() => ran = true);
        q.Flush(TimeSpan.FromSeconds(5)).Should().BeTrue();

        errors.Should().ContainSingle().Which.Message.Should().Be("disk full");
        ran.Should().BeTrue();
    }

    [Fact]
    public void Dispose_WorkStillQueued_RunsItFirst()
    {
        var ran = 0;
        var q = new WorkQueue("TestWork", _ => { });
        q.Post(() => Thread.Sleep(50));
        for (var i = 0; i < 5; i++) q.Post(() => ran++);
        q.Dispose();

        ran.Should().Be(5);
    }

    [Fact]
    public void Post_AfterDispose_RunsOnTheCaller()
    {
        var q = new WorkQueue("TestWork", _ => { });
        q.Dispose();
        var thread = 0;
        q.Post(() => thread = Environment.CurrentManagedThreadId);

        thread.Should().Be(Environment.CurrentManagedThreadId);
        q.Pending.Should().Be(0);
    }
}
