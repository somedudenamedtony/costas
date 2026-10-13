// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Integrations.Lotw;

namespace Ft8Client.Integrations.Tests;

public class LotwTests
{
    private const string Report = """
        ARRL Logbook of the World Status Report
        <PROGRAMID:4>LoTW
        <APP_LoTW_LASTQSL:19>2026-10-11 18:22:05
        <APP_LoTW_NUMREC:1>2
        <eoh>
        <CALL:5>JA1AB
        <BAND:3>20M
        <MODE:3>FT8
        <QSO_DATE:8>20261010
        <TIME_ON:6>201500
        <QSL_RCVD:1>Y
        <QSLRDATE:8>20261011
        <DXCC:3>339
        <eor>
        <CALL:5>KL7RA
        <BAND:3>40M
        <MODE:4>MFSK
        <APP_LoTW_MODE:3>FT4
        <QSO_DATE:8>20261009
        <TIME_ON:6>063000
        <QSL_RCVD:1>Y
        <eor>
        """;

    [Fact]
    public void Parse_Report_ReadsRecordsAndLastQsl()
    {
        var r = LotwReportClient.Parse(Report);
        r.Ok.Should().BeTrue();
        r.LastQsl.Should().Be("2026-10-11 18:22:05");
        r.Records.Select(x => x["call"]).Should().Equal("JA1AB", "KL7RA");
        r.Records[1]["app_lotw_mode"].Should().Be("FT4");
    }

    [Fact]
    public void Parse_HtmlPage_SaysLoginFailed()
    {
        var r = LotwReportClient.Parse("<html><body>Username/password incorrect</body></html>");
        r.Ok.Should().BeFalse();
        r.Error.Should().Contain("login name or password");
    }

    [Fact]
    public void BuildUrl_EscapesAndAsksForQslsSince()
    {
        var url = LotwReportClient.BuildUrl("W7LIT", "p&ss word", "2026-10-11 18:22:05", "W7LIT");
        url.Should().StartWith("https://lotw.arrl.org/lotwuser/lotwreport.adi?login=W7LIT&password=p%26ss%20word&qso_query=1&qso_qsl=yes");
        url.Should().Contain("qso_qslsince=2026-10-11%2018%3A22%3A05").And.Contain("qso_owncall=W7LIT");
        LotwReportClient.BuildUrl("a", "b", null, null).Should().Contain("qso_qslsince=1900-01-01").And.NotContain("qso_owncall");
    }

    [Fact]
    public void Arguments_WithLocation_BatchUploadCompliant()
    {
        TqslUploader.Arguments("x.adi", "Home").Should().Equal("-x", "-d", "-u", "-a", "compliant", "-l", "Home", "x.adi");
        TqslUploader.Arguments("x.adi", null).Should().Equal("-x", "-d", "-u", "-a", "compliant", "x.adi");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    [InlineData(2, false)]
    [InlineData(11, false)]
    public void Uploaded_ExitCode_TrueWhenLotwHasTheContacts(int code, bool uploaded)
    {
        new TqslResult(code, TqslResult.Describe(code)).Uploaded.Should().Be(uploaded);
    }

    [Fact]
    public async Task Upload_MissingTqsl_SaysSo()
    {
        var r = await new TqslUploader(Core.Processes.NullProcessGuard.Instance).UploadAsync("/no/such/tqsl", "x.adi", null, TestContext.Current.CancellationToken);
        r.Code.Should().Be(-1);
        r.Message.Should().StartWith("Could not start TQSL");
    }
}
