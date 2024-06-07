var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapRazorSlicesEndpoints();

app.Run();
