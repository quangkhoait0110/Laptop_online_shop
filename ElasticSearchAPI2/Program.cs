using Couchbase;
using Couchbase.KeyValue;
using Nest;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ElasticSearchAPI2.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cấu hình Couchbase
builder.Services.AddSingleton<ICluster>(sp =>
{
    var clusterOptions = new ClusterOptions
    {
        UserName = "Administrator",
        Password = "NY3gCv4zqG"
    };
    return Cluster.ConnectAsync("http://10.70.123.77:8091", clusterOptions).Result;
});

builder.Services.AddSingleton<Couchbase.IBucket>(sp =>
{
    var cluster = sp.GetRequiredService<ICluster>();
    return cluster.BucketAsync("sohoa").Result;
});

builder.Services.AddSingleton<IScope>(sp =>
{
    var bucket = sp.GetRequiredService<Couchbase.IBucket>();
    return bucket.ScopeAsync("_default").Result;
});

builder.Services.AddSingleton<ICouchbaseCollection>(sp =>
{
    var scope = sp.GetRequiredService<IScope>();
    return scope.CollectionAsync("_default").Result;
});

// Cấu hình Elasticsearch
builder.Services.AddSingleton<ElasticClient>(sp =>
{
    var settings = new ConnectionSettings(new Uri("http://10.70.123.76:9200"))
        .DefaultIndex("products");
    return new ElasticClient(settings);
});
var services = builder.Services;

// Add CORS
services.AddCors(options =>{
    options.AddPolicy("AllowSpecificOrigin",
     builder =>{
        builder.WithOrigins("http://localhost:4200")
               .AllowAnyHeader()
               .AllowAnyMethod();
     });
});
// Thêm DataSyncService
builder.Services.AddHostedService<DataSyncService>();
builder.Services.AddScoped<SearchService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.UseCors("AllowSpecificOrigin");

app.Run();