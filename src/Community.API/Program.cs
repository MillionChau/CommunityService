using Community.API.Hubs;
using Community.API.RealTime;
using Community.Application;
using Community.Application.Interfaces;
using Community.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SignalR realtime cho bài viết/bình luận (hub tại /hubs/community)
builder.Services.AddSignalR();

// CORS cho frontend dev (React 3000 / Vite 5173) — SignalR cần AllowCredentials
const string CorsPolicy = "DevRadarCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins("http://localhost:3000", "http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// Đăng ký Clean Architecture Layers
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Cầu realtime: Application gọi IRealTimeNotifier → SignalR (IHubContext là singleton)
builder.Services.AddSingleton<IRealTimeNotifier, SignalRNotifier>();

var app = builder.Build();

app.UseMiddleware<Community.API.Middleware.ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
}

app.UseCors(CorsPolicy);

app.UseHttpsRedirection();

// Thứ tự bắt buộc: Authentication trước Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Auto-migrate database on startup
app.Services.MigrateDatabase();

// Hub realtime: /hubs/community (JWT qua ?access_token=... cho WebSocket)
app.MapHub<CommunityHub>("/hubs/community");
app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy", service = "CommunityService", timestamp = DateTime.UtcNow }));

app.Run();
