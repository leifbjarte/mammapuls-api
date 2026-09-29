using Mammapuls.Api.Auth;
using Mammapuls.Api.Endpoints;
using Mammapuls.Api.Media;
using Mammapuls.Api.OpenApi;
using Mammapuls.Api.Users;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddMammapulsAuth();
builder.AddUserStore();
builder.AddMediaStorage();

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
    .MapMeEndpoints();

app.MapDefaultEndpoints();

app.Run();
