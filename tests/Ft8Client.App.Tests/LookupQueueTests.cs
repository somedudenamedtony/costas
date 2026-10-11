// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Services;

namespace Ft8Client.App.Tests;

public class LookupQueueTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Request_SameCallTwice_QueuedOnce()
    {
        var q = new LookupQueue();
        q.Request("W9RDL", false, Now).Should().BeTrue();
        q.Request("w9rdl", false, Now).Should().BeFalse();
        q.Count.Should().Be(1);
    }

    [Fact]
    public void Request_SettledCall_NeverQueuedAgain()
    {
        var q = new LookupQueue();
        q.Request("W9RDL", false, Now);
        q.Next().Should().Be("W9RDL");
        q.Settle("W9RDL");

        q.Request("W9RDL", true, Now.AddHours(5)).Should().BeFalse("each call is asked about once per run");
        q.Next().Should().BeNull();
    }

    [Fact]
    public void Request_SettledFromCacheBeforeAsked_NotQueued()
    {
        var q = new LookupQueue();
        q.Settle("N4OPI");
        q.Request("N4OPI", false, Now).Should().BeFalse();
    }

    [Fact]
    public void Request_Urgent_GoesToFront()
    {
        var q = new LookupQueue();
        q.Request("W9RDL", false, Now);
        q.Request("N4OPI", false, Now);
        q.Request("HK3W", true, Now);
        q.Request("N4OPI", true, Now).Should().BeTrue("an urgent request moves a waiting call forward");

        q.Next().Should().Be("N4OPI");
        q.Next().Should().Be("HK3W");
        q.Next().Should().Be("W9RDL");
    }

    [Fact]
    public void Failed_RetriedOnlyAfterTheWait()
    {
        var q = new LookupQueue();
        q.Request("W9RDL", false, Now);
        q.Next();
        q.Failed("W9RDL", Now.AddMinutes(10));

        q.Request("W9RDL", true, Now.AddMinutes(9)).Should().BeFalse();
        q.Request("W9RDL", true, Now.AddMinutes(10)).Should().BeTrue();
    }

    [Fact]
    public void Request_Full_DropsBackgroundRequestsNotUrgentOnes()
    {
        var q = new LookupQueue(capacity: 2);
        q.Request("K1AAA", false, Now);
        q.Request("K1BBB", false, Now);
        q.Request("K1CCC", false, Now).Should().BeFalse();
        q.Request("K1DDD", true, Now).Should().BeTrue();

        q.Count.Should().Be(2);
        q.Next().Should().Be("K1DDD");
        q.Next().Should().Be("K1AAA");
    }
}
