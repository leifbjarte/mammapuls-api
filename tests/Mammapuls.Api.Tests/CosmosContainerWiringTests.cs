using Mammapuls.Api.CheckIns;
using Mammapuls.Api.Onboarding;
using Mammapuls.Api.Users;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Mammapuls.Api.Tests;

public sealed class CosmosContainerWiringTests
{
    [Fact]
    public void EachStoreGetsItsOwnContainer()
    {
        var builder = Host.CreateApplicationBuilder();
        foreach (var name in new[] { "users", "onboarding", "checkins" })
        {
            builder.Configuration[$"ConnectionStrings:{name}"] =
                $"AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==;Database=mammapuls;Container={name}";
        }

        builder.AddUserStore();
        builder.AddOnboardingStore();
        builder.AddCheckInStore();
        using var provider = builder.Services.BuildServiceProvider();

        foreach (var name in new[] { "users", "onboarding", "checkins" })
        {
            Assert.Equal(name, provider.GetRequiredKeyedService<Container>(name).Id);
        }

        var checkIns = provider.GetRequiredService<ICheckInStore>();
        Assert.IsType<CosmosCheckInStore>(checkIns);
        Assert.IsType<CosmosOnboardingStore>(provider.GetRequiredService<IOnboardingStore>());
        Assert.IsType<CosmosUserStore>(provider.GetRequiredService<IUserStore>());
    }
}
