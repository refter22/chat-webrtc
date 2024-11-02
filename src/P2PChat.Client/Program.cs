using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using P2PChat.Client;
using P2PChat.Client.Services;
using P2PChat.Client.Services.WebRTC;
using P2PChat.Client.ViewModels;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<SignalRService>();
builder.Services.AddScoped<WebRTCService>();
builder.Services.AddScoped<FileTransferManager>();
builder.Services.AddScoped<ChatViewModel>();

await builder.Build().RunAsync();
