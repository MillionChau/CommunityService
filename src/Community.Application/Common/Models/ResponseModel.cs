namespace Community.Application.Common.Models;

public class ResponseModel<T>
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public int? Code { get; set; }

    public static ResponseModel<T> Success(T? data = default, string message = "Success")
        => new() { Data = data, Code = 1, IsSuccess = true, Message = message };

    public static ResponseModel<T> Error(string message = "Error")
        => new() { Data = default, Code = 0, IsSuccess = false, Message = message };

    public static ResponseModel<T> Error(int code, string message = "Error")
        => new() { Data = default, Code = code, IsSuccess = false, Message = message };
}
