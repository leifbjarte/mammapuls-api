using System.Text.Json.Nodes;
using Mammapuls.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Mammapuls.Api.OpenApi;

public static class OpenApiExtensions
{
    public const string ScalarRoute = "/scalar";
    public const string TestEnvironmentName = "Test";

    private const string SessionScheme = "session";
    private const string CsrfHeaderValue = "XMLHttpRequest";

    private const string Description = """
        Sign in: open `/api/v1/auth/login?returnUrl=/scalar/v1` in this browser (HTTPS) to log in with Vipps.
        The API then sets an HttpOnly session cookie that the browser sends automatically on same-origin requests,
        including requests made from this API reference — no token needs to be entered.

        Non-GET requests must carry the `X-Requested-With` header (CSRF guard); it is documented as a
        required header parameter and prefilled with `XMLHttpRequest`.
        """;

    public static WebApplicationBuilder AddMammapulsOpenApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer(DescribeDocument);
            options.AddOperationTransformer(DescribeSessionAndCsrf);
        });
        return builder;
    }

    public static bool IsApiReferenceEnabled(this IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment(TestEnvironmentName);

    public static WebApplication MapMammapulsApiReference(this WebApplication app)
    {
        if (!app.Environment.IsApiReferenceEnabled())
        {
            return app;
        }

        app.MapOpenApi().AllowAnonymous();
        // The Agent chat sends the API document to Scalar's hosted service.
        app.MapScalarApiReference(ScalarRoute, options => options
                .WithTitle("Mammapuls API")
                .DisableAgent())
            .AllowAnonymous();

        return app;
    }

    private static Task DescribeDocument(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "Mammapuls API";
        document.Info.Description = Description;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SessionScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = AuthConstants.CookieName,
            Description = "HttpOnly session cookie set by the Vipps login. The browser sends it automatically; leave the value empty.",
        };

        return Task.CompletedTask;
    }

    private static Task DescribeSessionAndCsrf(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        // Every endpoint without AllowAnonymous falls under the authenticated fallback policy.
        if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SessionScheme, context.Document)] = [],
            });
        }

        var method = context.Description.HttpMethod ?? HttpMethods.Get;
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method) && !HttpMethods.IsTrace(method))
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = AuthConstants.CsrfHeaderName,
                In = ParameterLocation.Header,
                Required = true,
                Description = "CSRF guard: any non-empty value. Browsers only send it cross-origin after a CORS preflight.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Default = JsonValue.Create(CsrfHeaderValue) },
                Example = JsonValue.Create(CsrfHeaderValue),
            });
        }

        return Task.CompletedTask;
    }
}
