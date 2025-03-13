using Frontend.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using QuizFrontend;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register HttpClient as scoped
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://quizapi-frontend-gycfh5bhhfe4hph5.germanywestcentral-01.azurewebsites.net/") });

// Refactor SignalRClientService to scoped instead of singleton
builder.Services.AddScoped<SignalRClientService>();

await builder.Build().RunAsync();
