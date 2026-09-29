using Mammapuls.Api.Auth;
using Mammapuls.Api.Endpoints;
using Mammapuls.Api.Media;
using Mammapuls.Api.Users;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddMammapulsAuth();
builder.AddUserStore();
builder.AddMediaStorage();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMammapulsAuth();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapGroup("/api/v1")
    .MapPingEndpoints()
    .MapAuthEndpoints()
    .MapMeEndpoints();

app.MapDefaultEndpoints();

app.Run();
