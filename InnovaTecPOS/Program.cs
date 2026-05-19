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
builder.Services.AddScoped<DailyReportPdfService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddHostedService<DailyReportScheduler>();

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

app.MapGet("/favicon.ico", async (ConfiguracionService config, IWebHostEnvironment env) => {
    var logo = await config.GetSettingAsync("Empresa_Logo");
    var path = string.IsNullOrEmpty(logo) ? "default_favicon.png" : logo;
    var physicalPath = System.IO.Path.Combine(env.WebRootPath, path.TrimStart('/'));
    if (System.IO.File.Exists(physicalPath)) {
        return Results.File(physicalPath, "image/png");
    }
    return Results.File(System.IO.Path.Combine(env.WebRootPath, "default_favicon.png"), "image/png");
});

app.MapGet("/favicon.png", async (ConfiguracionService config, IWebHostEnvironment env) => {
    var logo = await config.GetSettingAsync("Empresa_Logo");
    var path = string.IsNullOrEmpty(logo) ? "default_favicon.png" : logo;
    var physicalPath = System.IO.Path.Combine(env.WebRootPath, path.TrimStart('/'));
    if (System.IO.File.Exists(physicalPath)) {
        return Results.File(physicalPath, "image/png");
    }
    return Results.File(System.IO.Path.Combine(env.WebRootPath, "default_favicon.png"), "image/png");
});

// Actualizar la contraseña en la base de datos temporalmente para validación
using (var scope = app.Services.CreateScope())
{
    var configService = scope.ServiceProvider.GetRequiredService<ConfiguracionService>();
    await configService.UpdateSettingsBatchAsync(new Dictionary<string, string>
    {
        { "Correo_Password", "yjgw jqsc guja ztdh" }
    });
}

// Endpoint temporal para validar el envío del correo
app.MapGet("/test-email", async (EmailService emailService, DailyReportPdfService pdfService, ConfiguracionService configService) => {
    var logs = new System.Text.StringBuilder();
    logs.AppendLine("--- TEST DE AUTENTICACION DE CORREOS ---");
    
    var rawPassword = "yjgw jqsc guja ztdh";
    var cleanPassword = rawPassword.Replace(" ", "").Trim();
    var testEmails = new[] { "darennajippineda@gmail.com", "daren.castillofurioso@gmail.com" };
    
    foreach (var email in testEmails)
    {
        logs.AppendLine($"Probando remitente: '{email}' con contraseña '{rawPassword}' (limpia: '{cleanPassword}')...");
        try
        {
            using (var client = new MailKit.Net.Smtp.SmtpClient())
            {
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                try
                {
                    await client.ConnectAsync("smtp.gmail.com", 465, MailKit.Security.SecureSocketOptions.SslOnConnect);
                    logs.AppendLine("  [✓] Conectado exitosamente al puerto 465.");
                }
                catch (Exception ex465)
                {
                    logs.AppendLine($"  [!] Puerto 465 falló: {ex465.Message}. Intentando puerto 587...");
                    await client.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                    logs.AppendLine("  [✓] Conectado exitosamente al puerto 587.");
                }

                await client.AuthenticateAsync(email, cleanPassword);
                logs.AppendLine($"  [✓] ¡AUTENTICACIÓN EXITOSA para {email}!");
                
                // Intentar enviar un correo de prueba rápido para confirmar
                var message = new MimeKit.MimeMessage();
                message.From.Add(new MimeKit.MailboxAddress("InnovaTecPOS Test", email));
                message.To.Add(new MimeKit.MailboxAddress("Administración", "daren.castillofurioso@gmail.com"));
                message.Subject = "Test de Conectividad SMTP";
                message.Body = new MimeKit.TextPart("plain") { Text = "Este es un correo de prueba de autenticación exitosa." };
                
                await client.SendAsync(message);
                logs.AppendLine("  [✓] Correo de prueba enviado exitosamente.");
                
                await client.DisconnectAsync(true);
            }
        }
        catch (Exception ex)
        {
            logs.AppendLine($"  [✗] Error de autenticación para {email}: {ex.Message}");
        }
    }
    
    var result = logs.ToString();
    Console.WriteLine(result);
    return Results.Text(result, "text/plain; charset=utf-8");
});

app.MapGet("/show-config", async (ConfiguracionService configService) => {
    try {
        var settings = await configService.GetAllSettingsAsync();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("--- CONFIGURACIONES EN BASE DE DATOS ---");
        foreach (var kvp in settings)
        {
            sb.AppendLine($"{kvp.Key}: {kvp.Value}");
        }
        return Results.Text(sb.ToString(), "text/plain; charset=utf-8");
    } catch (Exception ex) {
        return Results.Problem(ex.ToString());
    }
});

app.Run();
