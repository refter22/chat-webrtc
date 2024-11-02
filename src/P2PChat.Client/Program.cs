using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using P2PChat.Client;
using P2PChat.Client.Services;
using P2PChat.Client.Services.WebRTC;
using P2PChat.Client.ViewModels;
using P2PChat.Client.Services.Storage;
using P2PChat.Client.Store;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<SignalRService>();
builder.Services.AddSingleton<WebRTCService>();
builder.Services.AddSingleton<FileTransferManager>();
builder.Services.AddSingleton<IStorageService, LocalStorageService>();
builder.Services.AddSingleton<IChatStore, ChatStore>();

builder.Services.AddScoped<ChatViewModel>();

await builder.Build().RunAsync();
