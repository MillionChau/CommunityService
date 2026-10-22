using Community.Application.Common.Models;
using Community.Application.DTOs.Reputation;
using Community.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Community.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReputationController : ApiControllerBase
{
    private readonly IReputationService _reputationService;
    private readonly ICurrentUserService _currentUser;

    public ReputationController(IReputationService reputationService, ICurrentUserService currentUser)
    {
        _reputationService = reputationService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Lấy thông tin điểm uy tín, cấp bậc và điểm tín nhiệm của người dùng hiện tại (FR-REP-01).
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ResponseModel<UserReputationDto>), 200)]
    public async Task<IActionResult> GetMyReputation(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
            return Unauthorized(ResponseModel<UserReputationDto>.Error("User not authenticated."));

        var result = await _reputationService.GetUserReputationAsync(_currentUser.UserId.Value, cancellationToken);
        return Ok(ResponseModel<UserReputationDto>.Success(result));
    }

    /// <summary>
    /// Lấy thông tin điểm uy tín và cấp bậc công khai của một người dùng bất kỳ.
    /// </summary>
    [HttpGet("user/{userId:guid}")]
    [ProducesResponseType(typeof(ResponseModel<UserReputationDto>), 200)]
    public async Task<IActionResult> GetUserReputation(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _reputationService.GetUserReputationAsync(userId, cancellationToken);
        return Ok(ResponseModel<UserReputationDto>.Success(result));
    }

    /// <summary>
    /// Lấy lịch sử biến động điểm uy tín và tín nhiệm của người dùng hiện tại (Audit Log).
    /// </summary>
    [HttpGet("history")]
    [Authorize]
    [ProducesResponseType(typeof(ResponseModel<List<ReputationTransactionDto>>), 200)]
    public async Task<IActionResult> GetMyHistory([FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
            return Unauthorized(ResponseModel<List<ReputationTransactionDto>>.Error("User not authenticated."));

        var result = await _reputationService.GetHistoryAsync(_currentUser.UserId.Value, limit, cancellationToken);
        return Ok(ResponseModel<List<ReputationTransactionDto>>.Success(result));
    }

    /// <summary>
    /// Quản trị viên áp dụng chế tài xử phạt người dùng (Warning, Sandbox, Mute, Ban).
    /// </summary>
    [HttpPost("admin/sanction")]
    [Authorize]
    [ProducesResponseType(typeof(ResponseModel<bool>), 200)]
    public async Task<IActionResult> ApplySanction([FromBody] SanctionRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return Forbid();

        var adminIdentifier = _currentUser.UserName ?? "Admin";
        await _reputationService.ApplySanctionAsync(
            request.UserId,
            request.SanctionLevel,
            request.DurationDays,
            request.Reason,
            adminIdentifier,
            cancellationToken);

        return Ok(ResponseModel<bool>.Success(true, "Sanction applied successfully."));
    }

    /// <summary>
    /// Quản trị viên gỡ bỏ chế tài và khôi phục quyền hạn (Ân xá).
    /// </summary>
    [HttpPost("admin/pardon")]
    [Authorize]
    [ProducesResponseType(typeof(ResponseModel<bool>), 200)]
    public async Task<IActionResult> PardonUser([FromBody] PardonRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return Forbid();

        await _reputationService.PardonUserAsync(
            request.UserId,
            request.Reason,
            request.RestorePoints,
            cancellationToken);

        return Ok(ResponseModel<bool>.Success(true, "User pardoned successfully."));
    }

    /// <summary>
    /// Kích hoạt tiến trình quét Inactivity Decay thủ công (Dành cho Admin/Testing).
    /// </summary>
    [HttpPost("admin/process-decay")]
    [Authorize]
    public async Task<IActionResult> ProcessDecay(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return Forbid();

        await _reputationService.ProcessInactivityDecayAsync(cancellationToken);
        return Ok(ResponseModel<bool>.Success(true, "Inactivity decay processed successfully."));
    }

    /// <summary>
    /// Kích hoạt tiến trình phục hồi TSS thụ động thủ công (Dành cho Admin/Testing).
    /// </summary>
    [HttpPost("admin/process-tss-recovery")]
    [Authorize]
    public async Task<IActionResult> ProcessTssRecovery(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return Forbid();

        await _reputationService.ProcessTssPassiveRecoveryAsync(cancellationToken);
        return Ok(ResponseModel<bool>.Success(true, "TSS passive recovery processed successfully."));
    }
}
