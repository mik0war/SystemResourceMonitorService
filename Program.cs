using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

class Program
{
    private static AlertService alertService;
    private static ResourceMonitor monitor;
    private static int interval;

    static async Task Main(string[] args)
    {
        Init();

        Log.Information("[State] === Служба мониторинга системных ресурсов запущена ===");

        // Обработка остановки приложения
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Log.Information("[State] Инициирован процесс завершения...");
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var logMessage = monitor.GetLogs();
                Log.Information(logMessage);
                Console.WriteLine($"[Process] {DateTime.Now}.  {logMessage}");

                // Ожидание до следующего сбора данных
                await Task.Delay(TimeSpan.FromSeconds(interval), cts.Token);
            }
        }
        catch (TaskCanceledException)
        {
            // Ожидаемое исключение при отмене задачи
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[State] Произошла неожиданная ошибка.");
            Console.WriteLine($"[ERROR] {ex.Message}");
        }
        finally
        {
            Log.Information("[State] === Служба мониторинга системных ресурсов остановлена ===");
            Log.CloseAndFlush();
        }
    }

    private static void Init()
    {
        // Настройка конфигурации
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", false, true)
            .Build();

        // Настройка Serilog
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.File(
                path: configuration["MonitoringSettings:LogFilePath"],
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")
            .CreateLogger();


        // Чтение настроек
        interval = int.Parse(configuration["MonitoringSettings:IntervalSeconds"]);
        float cpuThreshold = float.Parse(configuration["MonitoringSettings:Thresholds:Cpu"]);
        float memoryThreshold = float.Parse(configuration["MonitoringSettings:Thresholds:Memory"]);


        // Инициализация сервиса отправки email
        alertService = new AlertService(configuration);

        // Инициализация ресурсов
        var cpuResource = new IResource.ThresholdedValue(
            manager: new CPUCounter(
                new PerformanceCounter(
                    "Processor",
                    "% Processor Time",
                    "_Total"
                    )
                ),
            threshold: cpuThreshold,
            sendAction: (float cpuThreshold, float cpu) =>
            {
                string subject = "Оповещение.  Высокая загрузка CPU";
                string body = $"Загрузка CPU превысила порог в {cpuThreshold}%.\n" +
                    $"Текущая загрузка.  {cpu}%.";
                alertService.SendEmail(subject, body);
            }
        );

        var memoryResource = new IResource.ThresholdedValue(
            manager: new RamCounter(
                    new PerformanceCounter(
                        "Memory",
                        "Available MBytes"
                        )
                ),
            threshold: memoryThreshold,
            sendAction: (float memoryThreshold, float memory) =>
            {
                string subject = "Оповещение.  Низкое количество доступной памяти";
                string body = $"Доступная память опустилась ниже порога в {memoryThreshold} MB.\n" +
                $"Текущая доступная память.  {memory} MB.";
                alertService.SendEmail(subject, body);
            }
        );

        var diskResource = new IResource.Value(
            manager: new DiskCounter(
                new PerformanceCounter(
                    "PhysicalDisk",
                    "% Disk Time",
                    "_Total")
            )
            );

        var netResource = new IResource.Value(
            manager: new NetworkCounter(
                new PerformanceCounterCategory("Network Interface")
            )
        );


        monitor = new ResourceMonitor(
            [
            cpuResource,
            memoryResource,
            diskResource,
            netResource
            ]
        );
    }
}
