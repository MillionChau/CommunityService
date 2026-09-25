using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Posts.Commands.SharePost;

/// <summary>Kết quả chia sẻ bài viết: đường link chia sẻ + số đếm mới.</summary>
public record SharePostResult(Guid PostId, string ShareLink, int SharesCount);

public record SharePostCommand(Guid PostId) : IRequest<ResponseModel<SharePostResult>>;

public class SharePostCommandValidator : AbstractValidator<SharePostCommand>
{
    public SharePostCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty().WithMessage("PostId is required.");
    }
}

/// <summary>
/// Chia sẻ bài viết (FR-22 / UC-22): tăng SharesCount và trả về đường link chia sẻ
/// để client copy hoặc đẩy lên mạng xã hội. Cho phép cả Guest (theo ma trận quyền
/// SRS: Guest / User) — nhưng Guest không đăng nhập thì link vẫn hoạt động bình thường.
/// </summary>
public class SharePostCommandHandler : IRequestHandler<SharePostCommand, ResponseModel<SharePostResult>>
{
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealTimeNotifier _notifier;

    public SharePostCommandHandler(
        IPostRepository postRepository,
        IUnitOfWork unitOfWork,
        IRealTimeNotifier notifier)
    {
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
        _notifier = notifier;
    }

    public async Task<ResponseModel<SharePostResult>> Handle(SharePostCommand request, CancellationToken cancellationToken)
    {
        var post = await _postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post == null)
            throw new Common.Exceptions.NotFoundException("Post", request.PostId);

        post.SharesCount = (post.SharesCount ?? 0) + 1;
        await _postRepository.UpdateAsync(post, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        // Đường link chia sẻ chuẩn REST để client copy / chia sẻ mạng xã hội (FR-22)
        var shareLink = $"/posts/{post.Id}";

        var result = new SharePostResult(post.Id, shareLink, post.SharesCount ?? 0);

        // Realtime: cập nhật SharesCount cho mọi client đang xem
        await _notifier.NotifyFeedAsync("post-shared",
            new { postId = post.Id, sharesCount = result.SharesCount }, cancellationToken);
        await _notifier.NotifyPostAsync(post.Id, "post-shared",
            new { postId = post.Id, sharesCount = result.SharesCount }, cancellationToken);

        return ResponseModel<SharePostResult>.Success(result, "Post shared successfully.");
    }
}
