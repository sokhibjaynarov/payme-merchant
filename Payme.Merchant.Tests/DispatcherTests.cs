using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Newtonsoft.Json.Linq;
using Payme.Merchant;
using Payme.Merchant.Configuration;
using Payme.Merchant.Example.Services;
using Xunit;

public class DispatcherTests
{
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000));
    private readonly PaymeDispatcher _d;
    private const string Auth = "Basic UGF5Y29tOmtleTp3aXRoOmNvbG9u"; // Paycom:key:with:colon

    public DispatcherTests()
    {
        var svc = new InMemoryPaymeService();
        _d = new PaymeDispatcher(Options.Create(new PaymeOptions { Password = "key:with:colon" }), svc, svc, _clock);
    }

    private async Task<JObject> Call(string method, object p, string? auth = Auth)
    {
        var body = new JObject { ["jsonrpc"] = "2.0", ["id"] = 7, ["method"] = method, ["params"] = JObject.FromObject(p) };
        return JObject.Parse(await _d.DispatchAsync(auth, body.ToString()));
    }

    private static int? Err(JObject r) => (int?)r["error"]?["code"];
    private static object Acc(string id = "1001") => new { order_id = id };
    private long Now => _clock.GetUtcNow().ToUnixTimeMilliseconds();

    [Fact]
    public async Task WrongAuth_Is32504()
    {
        var bad = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("Paycom:nope"));
        Assert.Equal(-32504, Err(await Call("CheckPerformTransaction", new { amount = 100000, account = Acc() }, bad)));
        Assert.Equal(-32504, Err(await Call("CheckPerformTransaction", new { amount = 100000, account = Acc() }, null)));
    }

    [Fact]
    public async Task BadJson_Is32700() =>
        Assert.Equal(-32700, Err(JObject.Parse(await _d.DispatchAsync(Auth, "{oops"))));

    [Fact]
    public async Task UnknownMethodAndChangePassword_Are32601()
    {
        Assert.Equal(-32601, Err(await Call("Foo", new { })));
        Assert.Equal(-32601, Err(await Call("ChangePassword", new { password = "x" })));
    }

    [Fact]
    public async Task Check_ValidatesOrderAndAmount()
    {
        Assert.True((bool)(await Call("CheckPerformTransaction", new { amount = 100000, account = Acc() }))["result"]!["allow"]!);
        Assert.Equal(-31050, Err(await Call("CheckPerformTransaction", new { amount = 100000, account = Acc("999") })));
        Assert.Equal(-31001, Err(await Call("CheckPerformTransaction", new { amount = 1, account = Acc() })));
    }

    [Fact]
    public async Task FullFlow_CreatePerformCheck_AndIdempotency()
    {
        _clock.Advance(TimeSpan.FromSeconds(1));
        var c = await Call("CreateTransaction", new { id = "t1", time = 1_600_000_000_000, amount = 100000, account = Acc() });
        Assert.Equal(1, (int)c["result"]!["state"]!);
        Assert.Equal(Now, (long)c["result"]!["create_time"]!); // server time, not Payme's

        var again = await Call("CreateTransaction", new { id = "t1", time = 1_600_000_000_000, amount = 100000, account = Acc() });
        Assert.Equal(Now, (long)again["result"]!["create_time"]!);

        Assert.Equal(-31099, Err(await Call("CreateTransaction", new { id = "t2", time = 1, amount = 100000, account = Acc() }))); // order already has a pending transaction

        var p = await Call("PerformTransaction", new { id = "t1" });
        Assert.Equal(2, (int)p["result"]!["state"]!);
        var p2 = await Call("PerformTransaction", new { id = "t1" });
        Assert.Equal((long)p["result"]!["perform_time"]!, (long)p2["result"]!["perform_time"]!);

        Assert.Equal(2, (int)(await Call("CheckTransaction", new { id = "t1" }))["result"]!["state"]!);
        Assert.Equal(-31003, Err(await Call("CheckTransaction", new { id = "nope" })));
    }

    [Fact]
    public async Task Cancel_Created_ThenIdempotent()
    {
        await Call("CreateTransaction", new { id = "t1", time = 1, amount = 100000, account = Acc() });
        var r = await Call("CancelTransaction", new { id = "t1", reason = 3 });
        Assert.Equal(-1, (int)r["result"]!["state"]!);
        var r2 = await Call("CancelTransaction", new { id = "t1", reason = 3 });
        Assert.Equal((long)r["result"]!["cancel_time"]!, (long)r2["result"]!["cancel_time"]!);
        Assert.Equal(3, (int)(await Call("CheckTransaction", new { id = "t1" }))["result"]!["reason"]!);
        Assert.Equal(-31008, Err(await Call("PerformTransaction", new { id = "t1" })));
    }

    [Fact]
    public async Task Cancel_AfterPerform_RefusedByDefault()
    {
        await Call("CreateTransaction", new { id = "t1", time = 1, amount = 100000, account = Acc() });
        await Call("PerformTransaction", new { id = "t1" });
        Assert.Equal(-31007, Err(await Call("CancelTransaction", new { id = "t1", reason = 5 })));
    }

    [Fact]
    public async Task Timeout_CancelsWithReason4()
    {
        await Call("CreateTransaction", new { id = "t1", time = 1, amount = 100000, account = Acc() });
        _clock.Advance(TimeSpan.FromHours(12) + TimeSpan.FromSeconds(1));
        Assert.Equal(-31008, Err(await Call("PerformTransaction", new { id = "t1" })));
        var ch = await Call("CheckTransaction", new { id = "t1" });
        Assert.Equal(-1, (int)ch["result"]!["state"]!);
        Assert.Equal(4, (int)ch["result"]!["reason"]!);

        // A new transaction for the same order is allowed once the old one expired.
        Assert.Null(Err(await Call("CreateTransaction", new { id = "t2", time = 2, amount = 100000, account = Acc() })));
    }

    [Fact]
    public async Task GetStatement_ReturnsRange()
    {
        await Call("CreateTransaction", new { id = "a", time = 100, amount = 100000, account = Acc("1001") });
        await Call("CreateTransaction", new { id = "b", time = 500, amount = 500000, account = Acc("1002") });
        var r = await Call("GetStatement", new { from = 50, to = 200 });
        var list = (JArray)r["result"]!["transactions"]!;
        Assert.Single(list);
        Assert.Equal("a", (string)list[0]["id"]!);
        Assert.Equal("1001", (string)list[0]["account"]!["order_id"]!);
    }
}
