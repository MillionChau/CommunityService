namespace Community.Application.Common.Exceptions;

/// <summary>
/// Ném khi người dùng cố gắng thao tác trên tài nguyên không thuộc quyền sở hữu của mình
/// (ví dụ: sửa bài viết của người khác — SRS phân quyền F5 chỉ dành cho author/Admin).
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException() : base("You do not have permission to perform this action.") { }

    public ForbiddenException(string message) : base(message) { }
}
