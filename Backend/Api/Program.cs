using System.Text;
using Api.Data;
using Api.Models;
using Api.Repositories;
using Api.Services;
using Api.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

// Healthcheck Docker dùng .NET có sẵn trong image, không cài curl vào runtime.
if (args.Contains("--healthcheck"))
{
    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        return (await http.GetAsync("http://127.0.0.1:5000/health")).IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException) { return 1; }
    catch (TaskCanceledException) { return 1; }
}

var builder = WebApplication.CreateBuilder(args);
var publicHosting = builder.Configuration.GetValue<bool>("PUBLIC_HOSTING");
if (publicHosting) builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("guest-create", _ => RateLimitPartition.GetFixedWindowLimiter("guests", _ => new FixedWindowRateLimiterOptions { PermitLimit=30, Window=TimeSpan.FromMinutes(1), QueueLimit=0, AutoReplenishment=true }));
    options.AddPolicy("game-ticket", context => RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit=120, Window=TimeSpan.FromMinutes(1), QueueLimit=0, AutoReplenishment=true }));
    options.AddPolicy("room-create", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ?? "anonymous", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    // Một hạn mức chung cho register/login; không tin X-Forwarded-For từ client public.
    // Local dùng NoLimiter để giữ hành vi cũ. Tên policy tồn tại ở cả hai môi trường.
    options.AddPolicy("public-auth", _ => publicHosting
        ? RateLimitPartition.GetFixedWindowLimiter("auth", _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true })
        : RateLimitPartition.GetNoLimiter("local"));
});
var connection = builder.Configuration["Mongo:ConnectionString"]
    ?? throw new InvalidOperationException("Missing Mongo connection");
var jwt = new JwtOptions();
builder.Configuration.GetSection(JwtOptions.SectionName).Bind(jwt);
jwt.Validate(); // Dừng sớm khi thiếu secret; không chạy API với key mặc định/yếu.

// Data -> Repository -> Service -> Controller. DI cấp dependency cho từng lớp.
builder.Services.AddSingleton<IMongoClient>(new MongoClient(connection));
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IGuestRepository, GuestRepository>();
builder.Services.AddSingleton<IMatchRepository, MatchRepository>();
builder.Services.AddSingleton<IOptions<JwtOptions>>(Options.Create(jwt));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<GuestService>();
builder.Services.AddScoped<IdentityResolver>();
builder.Services.AddHttpClient<RoomControlClient>(client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer(); // Đưa cả minimal endpoint /health vào Swagger.
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Jungle Maze Account, Guests & Rooms API",
        Version = "v1",
        Description = "Test: register → login → Authorize → POST /api/rooms → join UDP bằng roomCode 6 ký tự. Hỗ trợ guest profile qua POST /api/guests, game tickets qua POST /api/game-tickets, và xem lịch sử trận đấu qua GET /api/me/matches."
    });
    options.IncludeXmlComments(typeof(AuthService).Assembly);
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Dán accessToken từ login, không thêm tiền tố Bearer. Swagger tự gửi header Authorization."
    });
    options.AddSecurityDefinition("GuestProof", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Guest-Credential",
        Description = "Opaque 256-bit credential từ POST /api/guests. Dùng làm proof cho /api/game-tickets và /api/me/matches."
    });
    options.OperationFilter<BearerSecurityOperationFilter>();
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // Giữ claim "sub" đúng tên để API/GameServer đọc nhất quán.
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
var app = builder.Build();

// Tạo unique index trước khi nhận request. Nếu dữ liệu cũ trùng tên, startup báo lỗi,
// không tự xóa dữ liệu hoặc nhận đăng ký khi chưa bảo đảm tính duy nhất.
await app.Services.GetRequiredService<IUserRepository>().EnsureIndexesAsync();
await app.Services.GetRequiredService<IGuestRepository>().EnsureIndexesAsync();
await app.Services.GetRequiredService<IMatchRepository>().EnsureIndexesAsync();

// Trả lỗi chung thay vì lộ stack trace/connection string cho client.
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (Exception e) when ((e is MongoException or TimeoutException) && !context.Response.HasStarted)
    { context.Response.StatusCode=503; await context.Response.WriteAsJsonAsync(new { error="DatabaseUnavailable" }); }
});
if (publicHosting)
{
    // Content-Length cho phép trả 413 sớm, trước JSON formatter hoặc password hashing.
    // Kestrel vẫn giới hạn khi body dùng chunked và chưa biết độ dài ngay từ header.
    app.Use(async (context, next) =>
    {
        if (context.Request.ContentLength > 16 * 1024)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        await next(context);
    });
}

// Development mặc định có UI. Docker demo bật qua Swagger__Enabled; đặt false để tắt.
// Swagger chỉ mô tả/call API, không thay đổi hoặc bỏ kiểm tra JWT của endpoint.
if (app.Configuration.GetValue<bool?>("Swagger:Enabled") ?? app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("v1/swagger.json", "Jungle Maze API v1");
        options.DocumentTitle = "Jungle Maze — Test API";
        options.EnableTryItOutByDefault();
        // Không bật EnablePersistAuthorization: JWT chỉ giữ trong phiên UI, không lưu localStorage.
    });
}
app.UseRouting();
app.UseAuthentication(); // Xác minh token trước khi kiểm tra quyền truy cập.
app.UseRateLimiter(); // Policy room-create cần sub đã xác minh để chia quota theo user.
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", async (MongoDbContext context, CancellationToken cancellationToken) =>
{
    try
    {
        await context.Database.RunCommandAsync<BsonDocument>(
            new BsonDocument("ping", 1), cancellationToken: cancellationToken);
        return Results.Ok(new { status = "ok", database = "connected" });
    }
    catch (MongoException)
    {
        return Results.Json(
            new { status = "unhealthy", database = "unavailable" },
            statusCode: 503);
    }
})
    .WithTags("Health")
    .WithSummary("Kiểm tra API kết nối MongoDB")
    .WithDescription("Không cần JWT. 200 khi MongoDB ping được; 503 khi DB không khả dụng.")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status503ServiceUnavailable);

app.Run();
return 0;
