using InnovaTecPOS.Frontend.Components;
using InnovaTecPOS.Backend.Models;
using InnovaTecPOS.Backend.Services;
using InnovaTecPOS.Frontend.Services;
using Microsoft.EntityFrameworkCore;
using InnovaTecPOS.Backend.Interceptors;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = true);

// Registramos la Factoría de Contexto para evitar errores de concurrencia en Blazor Server
builder.Services.AddDbContextFactory<InnovaTecDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddInterceptors(new SqlSettingInterceptor()));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<SaleService>();
builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<ConfiguracionService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ExcelExportService>();
builder.Services.AddScoped<ILabelService, LabelService>();
builder.Services.AddScoped<AppState>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseStaticFiles();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
