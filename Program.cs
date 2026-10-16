using System.Text;
using AIWorkspace.API.Configuration;
using AIWorkspace.API.Models;
using AIWorkspace.API.Repositories;
using AIWorkspace.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---- Settings (bound from appsettings.json / user-secrets / environment variables) ----
builder.Services.Configure<JwtSettings>(cfg.GetSection("Jwt"));
builder.Services.Configure<SupabaseSettings>(cfg.GetSection("Supabase"));
builder.Services.Configure<MongoDBConfig>(cfg.GetSection("MongoDB"));
builder.Services.Configure<ClaudeSettings>(cfg.GetSection("Claude"));
builder.Services.Configure<ModeSettings>(cfg.GetSection("Modes"));

var jwt = cfg.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters. Set it with: dotnet user-secrets set \"Jwt:Key\" \"<long random string>\"");
var modes = cfg.GetSection("Modes").Get<ModeSettings>() ?? new ModeSettings();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- CORS: only the front-end origins may call the API from a browser ----
var origins = cfg.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000" };
builder.Services.AddCors(o => o.AddPolicy("AllowFrontend", p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

// ---- Authentication: validate our own JWT on every [Authorize] request ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;   // keep claim names like "sub" and "name" exactly as issued
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = "name",
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

// ---- Dependency injection ----
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

builder.Services.AddHttpClient("Supabase", (sp, c) =>
{
    var s = sp.GetRequiredService<IOptions<SupabaseSettings>>().Value;
    if (!string.IsNullOrEmpty(s.PublishableKey)) c.DefaultRequestHeaders.Add("apikey", s.PublishableKey);
});
builder.Services.AddHttpClient("Claude", c => c.Timeout = TimeSpan.FromMinutes(5));
builder.Services.AddScoped<IClaudeService, ClaudeService>();

if (modes.Auth.Equals("Supabase", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<IAuthService, SupabaseAuthService>();
else
    builder.Services.AddSingleton<IAuthService, LocalAuthService>();

if (modes.Storage.Equals("Mongo", StringComparison.OrdinalIgnoreCase))
{
    var mongo = cfg.GetSection("MongoDB").Get<MongoDBConfig>() ?? new MongoDBConfig();
    if (string.IsNullOrWhiteSpace(mongo.ConnectionString))
        throw new InvalidOperationException("Modes:Storage is \"Mongo\" but MongoDB:ConnectionString is empty.");
    builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongo.ConnectionString));
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongo.DatabaseName));
    builder.Services.AddSingleton<IRepository<TaskItem>>(sp => new MongoRepository<TaskItem>(sp.GetRequiredService<IMongoDatabase>(), "tasks"));
    builder.Services.AddSingleton<IRepository<ChatMessage>>(sp => new MongoRepository<ChatMessage>(sp.GetRequiredService<IMongoDatabase>(), "chat_messages"));
    builder.Services.AddSingleton<IRepository<MeetingNote>>(sp => new MongoRepository<MeetingNote>(sp.GetRequiredService<IMongoDatabase>(), "meetings"));
}
else
{
    builder.Services.AddSingleton<IRepository<TaskItem>, InMemoryRepository<TaskItem>>();
    builder.Services.AddSingleton<IRepository<ChatMessage>, InMemoryRepository<ChatMessage>>();
    builder.Services.AddSingleton<IRepository<MeetingNote>, InMemoryRepository<MeetingNote>>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", auth = modes.Auth, storage = modes.Storage }));

app.Run();

public partial class Program { }
