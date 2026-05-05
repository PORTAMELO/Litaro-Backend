var builder = WebApplication.CreateBuilder(args);

// Aquí irán tus servicios
// builder.Services.AddScoped<XxxService>();

var app = builder.Build();

// Aquí irán tus endpoints
// app.MapXxxEndpoints();

app.Run();