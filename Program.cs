using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Supabase;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
    });

builder.Services.AddSignalR().AddNewtonsoftJsonProtocol();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSignalR().AddNewtonsoftJsonProtocol();
var secretsConfig = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory()) // Set the base path where secrets.json is located
    .AddJsonFile("Secret/secret.json", optional: true, reloadOnChange: true)
    .Build();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<UserConnectionManager>();
builder.Services.AddSingleton<MessagesHub>();
//services.AddScoped<MessageController>();
builder.Services.AddSingleton<Supabase.Client>(provider =>
{
    var supabaseUrl = secretsConfig["Supabase:SupabaseUrl"];
    var supabaseKey = secretsConfig["Supabase:SupabaseKey"];
    return new Supabase.Client(
        supabaseUrl,
        supabaseKey,
        new SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = true
        });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        builder => builder
            .WithOrigins("https://fonkagram.netlify.app , http://localhost:5206, http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed((host)=>true)
            .AllowCredentials());
});
// Merge the contents of secrets.json into the configuration

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var value = secretsConfig.GetSection("AppSettings:Token").Value;
        
        if (value != null)
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(value)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true, // Ensure token has not expired
                ClockSkew = TimeSpan.Zero, // Optionally, set clock skew to zero to prevent any clock differences
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];

                    // If the request is for our SignalR hub...
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/messagesHub"))
                    {
                        // Read the token out of the query string
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        }
        else
        {
            throw new NotImplementedException("nullWhat");
        }
    });
builder.Services.AddControllers()
            .AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            });


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    });
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
        c.RoutePrefix = string.Empty; // Serve the Swagger UI at the root URL in production
    });
}



app.UseCors("AllowSpecificOrigins"); // Make sure this is before UseRouting

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        if (!context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
        {
            context.Response.Headers.Add("Access-Control-Allow-Origin", "https://fonkagram.netlify.app");
        }
        return Task.CompletedTask;
    });
    await next();
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseEndpoints(endpoints => {

    endpoints.MapControllers();
    endpoints.MapHub<MessagesHub>("/messagesHub");
});

app.Run();