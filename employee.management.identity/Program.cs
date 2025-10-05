var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(); // For controller-based APIs
builder.Services.AddEndpointsApiExplorer(); // Enables API explorer for tools like Swagger/OpenAPI
builder.Services.AddSwaggerGen(); // For generating OpenAPI documentation

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection(); // Redirects HTTP requests to HTTPS

app.UseAuthorization(); // Enables authorization middleware

app.MapControllers(); // Maps controller routes for controller-based APIs

// Example of a minimal API endpoint
app.MapGet("/hello", () => "Hello!");

app.Run();