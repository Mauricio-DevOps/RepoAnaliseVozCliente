using POCLeituradeVozCliente.Services;
using POCLeituradeVozCliente.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
builder.Services.Configure<OpenAiAudioOptions>(builder.Configuration.GetSection("OpenAiAudio"));
builder.Services.AddHttpClient("IaClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });
builder.Services.AddHttpClient("OpenAiAudioClient");
builder.Services.AddScoped<IExcelFeedbackReader, ExcelFeedbackReader>();
builder.Services.AddScoped<IIaAnalysisClient, IaAnalysisClient>();
builder.Services.AddScoped<IFeedbackAnalysisService, FeedbackAnalysisService>();
builder.Services.AddScoped<IOpenAiAudioTranscriptionService, OpenAiAudioTranscriptionService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
