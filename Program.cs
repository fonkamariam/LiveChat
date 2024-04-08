using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Supabase;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddControllers();
var secretsConfig = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory()) // Set the base path where secrets.json is located
    .AddJsonFile("Secret/secret.json", optional: true, reloadOnChange: true)
    .Build();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<Supabase.Client>(provider =>
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
        }
        else
        {
            throw new NotImplementedException("nullWhat");
        }
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();