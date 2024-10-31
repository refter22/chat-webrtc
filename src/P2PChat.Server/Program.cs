using P2PChat.Server.Hubs;
using P2PChat.Server.Services;
using P2PChat.Server.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.SetIsOriginAllowed(_ => true)
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();

app.MapHub<SignalingHub>("/signaling");

app.Run();