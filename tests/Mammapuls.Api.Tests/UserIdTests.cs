using Mammapuls.Api.Users;

namespace Mammapuls.Api.Tests;

public sealed class UserIdTests
{
    private const string ProdIssuer = "https://api.vipps.no/access-management-1.0/access/";
    private const string TestIssuer = "https://apitest.vipps.no/access-management-1.0/access/";
    private const string Subject = "c06c4afe-d9e1-4c5d-939a-177d752a0944";

    [Fact]
    public void CreateId_IsDeterministic()
    {
        Assert.Equal(CosmosUserStore.CreateId(ProdIssuer, Subject), CosmosUserStore.CreateId(ProdIssuer, Subject));
    }

    [Fact]
    public void CreateId_MatchesPinnedValue()
    {
        // Changing the derivation orphans every stored user; this vector was computed independently.
        Assert.Equal("3t5R30ZGIHtecvWUEkNelFr-DsLpM0q3URzGVaXHNgw", CosmosUserStore.CreateId(ProdIssuer, Subject));
    }

    [Fact]
    public void CreateId_DiffersAcrossIssuers()
    {
        Assert.NotEqual(CosmosUserStore.CreateId(ProdIssuer, Subject), CosmosUserStore.CreateId(TestIssuer, Subject));
    }

    [Fact]
    public void CreateId_DiffersAcrossSubjects()
    {
        Assert.NotEqual(CosmosUserStore.CreateId(ProdIssuer, Subject), CosmosUserStore.CreateId(ProdIssuer, Subject + "x"));
    }

    [Fact]
    public void CreateId_SeparatorInComponentsDoesNotCollide()
    {
        Assert.NotEqual(CosmosUserStore.CreateId("a|b", "c"), CosmosUserStore.CreateId("a", "b|c"));
    }

    [Fact]
    public void CreateId_IssuerIsNotNormalized()
    {
        Assert.NotEqual(CosmosUserStore.CreateId(ProdIssuer, Subject), CosmosUserStore.CreateId(ProdIssuer.TrimEnd('/'), Subject));
    }

    [Fact]
    public void CreateId_IsUrlSafeFixedLength()
    {
        Assert.Matches("^[A-Za-z0-9_-]{43}$", CosmosUserStore.CreateId(ProdIssuer, Subject));
    }

    [Theory]
    [InlineData("", Subject)]
    [InlineData(ProdIssuer, "")]
    public void CreateId_RejectsEmptyInput(string issuer, string subject)
    {
        Assert.Throws<ArgumentException>(() => CosmosUserStore.CreateId(issuer, subject));
    }
}
