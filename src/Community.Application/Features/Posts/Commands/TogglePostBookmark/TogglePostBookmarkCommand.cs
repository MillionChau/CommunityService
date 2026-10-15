using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Posts.Commands.TogglePostBookmark;

/// <summary>Kết quả toggle bookmark trả về cho client.</summary>
public record ToggleBookmarkResult(Guid PostId, bool IsBookmarked, int BookmarksCount);

public record TogglePostBookmarkCommand(Guid PostId, Guid? FolderId = null) : IRequest<ResponseModel<ToggleBookmarkResult>>;

public class TogglePostBookmarkCommandValidator : AbstractValidator<TogglePostBookmarkCommand>
{
    public TogglePostBookmarkCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty().WithMessage("PostId is required.");
    }
}

/// <summary>
/// Lưu / bỏ lưu bài viết (FR-20 / UC-20, UC-21). Toggle: thêm PostBookmark khi lưu,
/// xóa khi bỏ lưu. Hỗ trợ phân loại vào Folder nếu FolderId được chỉ định (UC-21).
/// </summary>
public class TogglePostBookmarkCommandHandler : IRequestHandler<TogglePostBookmarkCommand, ResponseModel<ToggleBookmarkResult>>
{
    private readonly IPostRepository _postRepository;
    private readonly IPostBookmarkRepository _bookmarkRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealTimeNotifier _notifier;

    public TogglePostBookmarkCommandHandler(
        IPostRepository postRepository,
        IPostBookmarkRepository bookmarkRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IRealTimeNotifier notifier)
    {
        _postRepository = postRepository;
        _bookmarkRepository = bookmarkRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<ResponseModel<ToggleBookmarkResult>> Handle(TogglePostBookmarkCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");
        var userId = _currentUser.UserId.Value;

        var post = await _postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post == null)
            throw new NotFoundException(nameof(Post), request.PostId);

        var existing = await _bookmarkRepository.GetByPostAndUserAsync(request.PostId, userId, cancellationToken);
        bool isBookmarked;
        if (existing == null)
        {
            await _bookmarkRepository.AddAsync(
                new PostBookmark
                {
                    Id = Guid.NewGuid(),
                    PostId = request.PostId,
                    UserId = userId,
                    FolderId = request.FolderId
                },
                cancellationToken);
            post.BookmarksCount = (post.BookmarksCount ?? 0) + 1;
            isBookmarked = true;
        }
        else if (request.FolderId.HasValue && existing.FolderId != request.FolderId)
        {
            // Cập nhật thư mục cho bookmark hiện có
            existing.FolderId = request.FolderId;
            await _bookmarkRepository.UpdateAsync(existing, cancellationToken);
            isBookmarked = true;
        }
        else
        {
            await _bookmarkRepository.DeleteAsync(existing.Id, cancellationToken);
            post.BookmarksCount = Math.Max((post.BookmarksCount ?? 0) - 1, 0);
            isBookmarked = false;
        }

        await _postRepository.UpdateAsync(post, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        var result = new ToggleBookmarkResult(post.Id, isBookmarked, post.BookmarksCount ?? 0);

        // Bookmark là hành động riêng tư của từng user — chỉ broadcast cho trang chi tiết
        await _notifier.NotifyPostAsync(post.Id, "post-bookmark-changed",
            new { postId = post.Id, bookmarksCount = result.BookmarksCount }, cancellationToken);

        return ResponseModel<ToggleBookmarkResult>.Success(result,
            isBookmarked ? "Post bookmarked." : "Bookmark removed.");
    }
}
