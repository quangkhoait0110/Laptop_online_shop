using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using ProductApi1.Models;
using ProductApi1.Services;
using ProductApi1.Filters;
using ProductApi1.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Cors;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Couchbase.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var configuration = builder.Configuration;

// Add services to the container.
var services = builder.Services;

// Add CORS
services.AddCors(options => {
    options.AddPolicy("AllowSpecificOrigin",
    builder => {
        builder.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Add Couchbase service
services.AddCouchbase(configuration.GetSection("Couchbase"));
services.AddSingleton<CouchbaseService>();
services.AddScoped<UserRepository>();
services.AddScoped<AuthService>();

builder.Services.AddControllers(options => {
    options.Filters.Add<GlobalExceptionFilter>();
});



// Add Redis caching
var redisConfiguration = configuration.GetSection("Redis").GetValue<string>("ConnectionString");
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConfiguration;
});

// Add Kafka Producer service
services.AddSingleton<KafkaProducerService>();

// Add Kafka Consumer
services.AddSingleton<IConsumer<string, string>>(provider =>
{
    var config = new ConsumerConfig
    {
        BootstrapServers = "10.70.123.76:31633",
        GroupId = "product-consumer-group",
        AutoOffsetReset = AutoOffsetReset.Earliest
    };
    return new ConsumerBuilder<string, string>(config).Build();
});

// Add Kafka Producer
services.AddSingleton<IProducer<string, string>>(provider =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = "10.70.123.76:31633"
    };
    return new ProducerBuilder<string, string>(config).Build();
});

// Add Kafka Consumer service
services.AddHostedService<KafkaConsumerService>();

// Add controllers
services.AddControllers();

services.AddSingleton<EmailService>();
services.AddSingleton<OrderService>();
services.AddSingleton<MinioService>();
services.AddTransient<SmtpClient>(_ => new SmtpClient{
    Host = "smtp.gmail.com",
    Port = 587,
    Credentials = new NetworkCredential("quangkhoadt0110@gmail.com","cdou liyv omel jocg"),
    EnableSsl = true,
    DeliveryMethod = SmtpDeliveryMethod.Network,
    UseDefaultCredentials = false
});

// Add JWT Authentication
services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidAudience = configuration["JWT:ValidAudience"],
        ValidIssuer = configuration["JWT:ValidIssuer"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:Secret"]!))
    };
});

// Build the app
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowSpecificOrigin");
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

app.Lifetime.ApplicationStopped.Register(() =>
{
    app.Services.GetRequiredService<ICouchbaseLifetimeService>().Close();
});