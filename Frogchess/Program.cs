using Frogchess.Hubs;
using Frogchess.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IGameService, GameService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// app.MapStaticAssets();

app.MapControllers();
app.MapHub<GameHub>("/gamehub");

app.Run();
