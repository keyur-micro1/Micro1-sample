using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.DataProtection;
using TdsCertificatePortal.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "TdsCertificatePortal-Keys")))
    .SetApplicationName("TdsCertificatePortal");
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".TdsCertificatePortal.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024;
});
builder.Services.AddSingleton<TempFileService>();
builder.Services.AddHostedService<TempCleanupHostedService>();
builder.Services.AddScoped<CertificateZipService>();
builder.Services.AddScoped<CsvEmployeeParser>();
builder.Services.AddScoped<MappingService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<SmtpEmailService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (app.Configuration.GetValue<int?>("HttpsRedirection:HttpsPort").HasValue)
{
    app.UseHttpsRedirection();
}
app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
