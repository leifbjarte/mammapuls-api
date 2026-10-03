using Mammapuls.Api.Auth;
using Mammapuls.Api.Endpoints;
using Mammapuls.Api.Media;
using Mammapuls.Api.Onboarding;
using Mammapuls.Api.OpenApi;
using Mammapuls.Api.Users;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddMammapulsAuth();
builder.AddUserStore();
builder.AddOnboardingStore();
builder.AddMediaStorage();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));

builder.Services.AddProblemDetails();
builder.AddMammapulsOpenApi();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMammapulsAuth();

app.MapMammapulsApiReference();

app.MapGroup("/api/v1")
    .MapPingEndpoints()
    .MapAuthEndpoints()
    .MapMeEndpoints()
    .MapOnboardingEndpoints();

app.MapDefaultEndpoints();

app.Run();
