using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

//const string proxiedUrl = "http://host.docker.internal:8000";
//const string proxiedUrl = "http://localhost:8000";
const string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
        b =>
        {
            b
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
            //builder.WithOrigins("https://test.rivalcoins.io", "https://rivalcoins.money");
        });
});

builder.Configuration.AddCommandLine(args).AddEnvironmentVariables();
var app = builder.Build();

var proxiedUrl = app.Configuration.GetValue<string>("PROXIED_URL");
Console.WriteLine($"Configuration: {proxiedUrl}");

app.UseCors();

// Configure the HTTP request pipeline.

//app.UseHttpsRedirection();

using var http = new HttpClient();

app.MapPost("/transactions", async (HttpRequest request) =>
{
    using var r = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("tx", request.Form["tx"].ToString()) });
    using var response = await http.PostAsync($"{proxiedUrl}/transactions", r);

    return TypedResults.Text(await response.Content.ReadAsStringAsync(), null, null, (int)response.StatusCode);
})
.RequireCors(MyAllowSpecificOrigins);

app.MapGet("/", async () =>
{
    using var result = await http.GetAsync(proxiedUrl);

    return TypedResults.Text(await result.Content.ReadAsStringAsync(), null, null, (int)result.StatusCode);
}).RequireCors(MyAllowSpecificOrigins);

app.MapGet("/{*requestUrl}", async (HttpRequest request, string requestUrl) =>
{
    var queryString = request.QueryString.ToString();
    using var result = await http.GetAsync($"{proxiedUrl}/{requestUrl}{queryString}");

    return TypedResults.Text(await result.Content.ReadAsStringAsync(), null, null, (int)result.StatusCode);
})
.RequireCors(MyAllowSpecificOrigins);

app.MapMethods("/{*requestUrl}", new[] { "OPTIONS" }, (HttpRequest request, string requestUrl) =>
{
    return Microsoft.AspNetCore.Http.Results.Ok();
})
.RequireCors(MyAllowSpecificOrigins);

app.Run();