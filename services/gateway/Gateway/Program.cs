using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// YARP reverse proxy — routes + clusters come from the "ReverseProxy" config section.
// A code transform forwards the authenticated user id as X-User-Id (config-based
// {claim:...} tokens are NOT supported by YARP, so this must be done in code).
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(ctx =>
    {
        ctx.AddRequestTransform(transform =>
        {
            // Anti-spoofing: never trust a client-supplied X-User-Id.
            transform.ProxyRequest.Headers.Remove("X-User-Id");

            // Forward the user id from the validated JWT (sub) when present.
            var userId = transform.HttpContext.User.FindFirst("sub")?.Value
                         ?? transform.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                transform.ProxyRequest.Headers.Add("X-User-Id", userId);
            }

            return ValueTask.CompletedTask;
        });
    });

// Centralized JWT validation. Tokens are signed by the Identity API with the SAME Jwt:Secret.
// MapInboundClaims = false keeps raw JWT claim names (so "sub" stays "sub").
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]
                    ?? throw new InvalidOperationException("Jwt:Secret is not configured"))),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(opt =>
    opt.AddPolicy("authenticated", p => p.RequireAuthenticatedUser()));

// CORS — the browser frontend is the only external origin allowed.
// AllowCredentials is required for the SignalR WebSocket.
builder.Services.AddCors(opt => opt.AddPolicy("Frontend", p =>
    p.WithOrigins(builder.Configuration["Frontend:Url"] ?? "http://localhost:5173")
     .AllowAnyMethod()
     .AllowAnyHeader()
     .AllowCredentials()));

var app = builder.Build();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();

app.Run();
