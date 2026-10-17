namespace AIVES.BusinessLogicLayer.Common;

/// <summary>
/// Envelope kết quả trả về chuẩn hoá của Business Layer.
/// </summary>
public class ServiceResult<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ServiceResult<T> SuccessResult(T data, string message = "Success")
        => new() { Success = true, Data = data, Message = message };

    public static ServiceResult<T> FailureResult(string message)
        => new() { Success = false, Message = message };
}
