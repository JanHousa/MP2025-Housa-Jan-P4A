using Microsoft.EntityFrameworkCore;
using QuizApi.Data;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using System.Text;
using Microsoft.Azure.SignalR;
using QuizApi.Hubs;
using System.Text.Json.Serialization;
using Serilog;
using Jose;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Konfigurace Serilog pro logy do souboru
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// CORS nastavení
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
    {
        builder.WithOrigins("https://quizapi-frontend-gycfh5bhhfe4hph5.germanywestcentral-01.azurewebsites.net/", "https://localhost:7183")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

// Pøidání SignalR
builder.Services.AddSignalR()
    .AddAzureSignalR(builder.Configuration["Azure:SignalR:ConnectionString"]);


// Swagger dokumentace
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Konfigurace databáze
builder.Services.AddDbContext<QuizAppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
    sqlOptions => sqlOptions.EnableRetryOnFailure()));

CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

var app = builder.Build();

// Pipeline konfigurace
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("CorsPolicy");

app.UseHttpsRedirection();
app.UseRouting();

// Custom JWT Middleware
app.UseCustomJwtMiddleware();

// Authorization Middleware
app.UseAuthorization();

app.MapHub<GameHub>("/gamehub");
app.MapControllers();

app.Run();
