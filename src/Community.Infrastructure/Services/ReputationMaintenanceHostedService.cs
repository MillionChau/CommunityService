using Community.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Community.Infrastructure.Services;

/// <summary>
/// Background Service định kỳ tự động chạy:
/// 1. Cơ chế trừ điểm ngủ đông (Inactivity Score Decay)
/// 2. Cơ chế phục hồi thụ động điểm TSS (+5 TSS / tuần không vi phạm)
/// 3. Chốt snapshot và vinh danh Bảng xếp hạng Tuần / Tháng
/// </summary>
public class ReputationMaintenanceHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReputationMaintenanceHostedService> _logger;
    private DateTime _lastRunDate = DateTime.MinValue;

    public ReputationMaintenanceHostedService(
        IServiceProvider serviceProvider,
        ILogger<ReputationMaintenanceHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReputationMaintenanceHostedService is starting...");

        // Delay 10 giây sau khi ứng dụng khởi động
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Chỉ chạy quét sâu 1 lần mỗi ngày (vào khung giờ đêm 00:00 - 04:00 UTC) hoặc khi lần đầu chạy
                if (now.Date > _lastRunDate.Date)
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var reputationService = scope.ServiceProvider.GetRequiredService<IReputationService>();

                        _logger.LogInformation("Executing daily reputation maintenance jobs (Decay, Recovery, Leaderboards)...");

                        // 1. Phục hồi thụ động TSS cho người dùng chuẩn mực
                        await reputationService.ProcessTssPassiveRecoveryAsync(stoppingToken);

                        // 2. Trừ suy giảm điểm đóng góp đối với tài khoản ngủ đông
                        await reputationService.ProcessInactivityDecayAsync(stoppingToken);

                        // 3. Nếu là ngày đầu tuần (Thứ Hai 00:00 UTC), chốt tuần vừa rồi
                        if (now.DayOfWeek == DayOfWeek.Monday)
                        {
                            await reputationService.CloseLeaderboardPeriodAsync("WEEKLY", stoppingToken);
                        }

                        // 4. Nếu là ngày mùng 1 đầu tháng, chốt tháng vừa rồi
                        if (now.Day == 1)
                        {
                            await reputationService.CloseLeaderboardPeriodAsync("MONTHLY", stoppingToken);
                        }
                    }

                    _lastRunDate = now;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during ReputationMaintenanceHostedService execution.");
            }

            // Quét định kỳ mỗi 1 giờ
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
