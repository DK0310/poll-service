using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Routes and clusters are loaded from the "ReverseProxy" config section (appsettings.json).
// YARP can't read a JWT claim from config, so the request transform below copies the caller's
// identity into headers in code.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(ctx =>
    {
        ctx.AddRequestTransform(transform =>
        {
            // NOTE: this is the security boundary. Downstream services trust X-User-Id/Role
            // blindly, so we drop whatever the client sent and set them only from the JWT we
            // just validated. Remove these two lines and anyone can impersonate any user.
            transform.ProxyRequest.Headers.Remove("X-User-Id");
            transform.ProxyRequest.Headers.Remove("X-User-Role");

            var userId = transform.HttpContext.User.FindFirst("sub")?.Value
                         ?? transform.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                transform.ProxyRequest.Headers.Add("X-User-Id", userId);
            }

            var role = transform.HttpContext.User.FindFirst("role")?.Value;
            if (!string.IsNullOrEmpty(role))
            {
                transform.ProxyRequest.Headers.Add("X-User-Role", role);
            }

            return ValueTask.CompletedTask;
        });
    });

// The gateway is the only place that verifies JWTs.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        // Keep the raw claim names ("sub", "role") instead of remapping them to .NET's URIs.
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            // NOTE: must be byte-for-byte identical to identity-api's Jwt:Secret. Identity signs
            // the token, the gateway verifies it with this same key; a mismatch = every request 401s.
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
{
    opt.AddPolicy("authenticated", p => p.RequireAuthenticatedUser());
    opt.AddPolicy("admin", p => p.RequireAuthenticatedUser().RequireClaim("role", "Admin"));
});

builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // NOTE: partitioned by client IP. Behind a proxy/load balancer (e.g. Render) every request
    // can arrive with the same IP, which lumps all users into one bucket. If you see unexpected
    // 429s in production, wire up ForwardedHeaders so this reads the real client IP.
    opt.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromSeconds(10),
                QueueLimit = 0
            }));

    // Tighter limit just for vote submits; routes opt in via "RateLimiterPolicy" in appsettings.json.
    opt.AddFixedWindowLimiter("vote-submit", limiterOpt =>
    {
        limiterOpt.PermitLimit = 5;
        limiterOpt.Window = TimeSpan.FromSeconds(10);
        limiterOpt.QueueLimit = 0;
    });
});

// Only the frontend origin may call the API. AllowCredentials is needed for the SignalR websocket.
builder.Services.AddCors(opt => opt.AddPolicy("Frontend", p =>
    p.WithOrigins(builder.Configuration["Frontend:Url"] ?? "http://localhost:5173")
     .AllowAnyMethod()
     .AllowAnyHeader()
     .AllowCredentials()));

var app = builder.Build();

// Order matters: authentication must run before authorization, and both before the proxy
// forwards the request (the transform above needs the validated user).
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapReverseProxy();

app.Run();
