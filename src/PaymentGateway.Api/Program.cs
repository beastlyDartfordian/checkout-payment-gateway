using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Payment request rejected; trace {TraceId}", context.HttpContext.TraceIdentifier);
        var errors = context.ModelState.Where(entry => entry.Value!.Errors.Count > 0)
            .ToDictionary(entry => entry.Key, _ => new[] { "Invalid or missing value." });
        return new BadRequestObjectResult(new ValidationProblemDetails(errors)
        {
            Status = 400,
            Title = "Payment request rejected.",
            Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
        });
    };
});
builder.Services.AddHttpClient("Bank", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Bank:BaseUrl"] ?? "http://localhost:8080/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddTransient<BankClient>();
builder.Services.AddSingleton<PaymentsRepository>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapHealthChecks("/health");
app.MapControllers();
app.Run();