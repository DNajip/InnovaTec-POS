using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace InnovaTecPOS.Backend.Services;

public class DailyReportScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DailyReportScheduler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("DAILY REPORT SCHEDULER: Iniciando planificador de reportes automáticos...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Calcular tiempo de retraso hasta las 7:00 PM del día actual o siguiente
                var now = DateTime.Now;
                var targetTime = new DateTime(now.Year, now.Month, now.Day, 19, 0, 0); // 7:00 PM (19:00)

                // Si ya pasaron las 7:00 PM hoy, programar para mañana a las 7:00 PM
                if (now >= targetTime)
                {
                    targetTime = targetTime.AddDays(1);
                }

                var delay = targetTime - now;
                Console.WriteLine($"DAILY REPORT SCHEDULER: Próxima ejecución programada para {targetTime:dd/MM/yyyy hh:mm tt} (Faltan: {delay.TotalHours:N2} horas).");

                // Esperar hasta la hora objetivo
                await Task.Delay(delay, stoppingToken);

                // Ejecutar proceso de generación y envío
                Console.WriteLine("DAILY REPORT SCHEDULER: Es hora de enviar el reporte consolidado diario. Ejecutando...");
                await ExecuteReportAndSendAsync();

                // Esperar un minuto adicional antes de la siguiente iteración para evitar disparos duplicados inmediatos
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // El servicio se está deteniendo
                Console.WriteLine("DAILY REPORT SCHEDULER: Planificador cancelado debido al apagado del servicio.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DAILY REPORT SCHEDULER ERROR: Error en el ciclo del planificador: {ex.Message}");
                // Esperar 15 minutos en caso de error crítico antes de reintentar para evitar bucles rápidos
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }
    }

    private async Task ExecuteReportAndSendAsync()
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            try
            {
                var pdfService = scope.ServiceProvider.GetRequiredService<DailyReportPdfService>();
                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

                // Generar reporte diario para la fecha actual (hoy)
                var dateToReport = DateTime.Today;
                Console.WriteLine($"DAILY REPORT SCHEDULER: Generando PDF diario para {dateToReport:dd/MM/yyyy}...");
                
                byte[] pdfBytes = await pdfService.GenerateDailyReportPdfAsync(dateToReport);

                Console.WriteLine("DAILY REPORT SCHEDULER: Enviando reporte por correo...");
                bool sent = await emailService.SendDailyReportEmailAsync(pdfBytes, dateToReport);

                if (sent)
                {
                    Console.WriteLine("DAILY REPORT SCHEDULER: Reporte diario procesado y enviado con éxito.");
                }
                else
                {
                    Console.WriteLine("DAILY REPORT SCHEDULER: No se pudo enviar el reporte por correo. Verifique logs y configuración.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DAILY REPORT SCHEDULER EXECUTION ERROR: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"DAILY REPORT SCHEDULER EXECUTION ERROR (Inner): {ex.InnerException.Message}");
                }
            }
        }
    }
}
