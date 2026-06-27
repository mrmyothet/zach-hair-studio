namespace ZachHairStudio.Shared;

public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public bool IsError => !IsSuccess;

    private EnumRespType Type { get; set; }
    public T Data { get; private set; } = default!;
    public string Message { get; private set; } = string.Empty;

    public bool IsValidationError() => Type == EnumRespType.ValidationError;
    public bool IsSystemError() => Type == EnumRespType.SystemError;
    public bool IsDataError() => Type == EnumRespType.Error;
    public bool IsNotFound() => Type == EnumRespType.NotFound;
    public bool IsDuplicateRecord() => Type == EnumRespType.DuplicateRecord;
    public bool IsInvalidData() => Type == EnumRespType.InvalidData;
    public bool IsBadRequest() => Type == EnumRespType.BadRequest;

    public static Result<T> Success(T data, string message = "Success") =>
        new Result<T> { IsSuccess = true, Type = EnumRespType.Success, Data = data, Message = message };

    public static Result<T> ValidationError(string message, T? data = default) =>
        new Result<T> { IsSuccess = false, Type = EnumRespType.ValidationError, Data = data, Message = message };

    public static Result<T> SystemError(string message, T? data = default) =>
        new Result<T> { IsSuccess = false, Type = EnumRespType.SystemError, Data = data, Message = message };

    public static Result<T> Error(string message = "Some Error Occurred", T? data = default) =>
        new Result<T> { IsSuccess = false, Type = EnumRespType.Error, Data = data, Message = message };

    public static Result<T> DuplicateRecordError(string message = "Duplicate Record", T? data = default) =>
        new Result<T> { IsSuccess = false, Type = EnumRespType.DuplicateRecord, Data = data, Message = message };

    public static Result<T> NotFoundError(string message = "Not Found", T? data = default) =>
        new Result<T> { IsSuccess = false, Type = EnumRespType.NotFound, Data = data, Message = message };

    public static Result<T> InvalidDataError(string message = "Invalid Data", T? data = default) =>
        new Result<T> { IsSuccess = false, Type = EnumRespType.InvalidData, Data = data, Message = message };

    public static Result<T> BadRequestError(string message = "Bad Request", T? data = default) =>
        new Result<T> { IsSuccess = false, Type = EnumRespType.BadRequest, Data = data, Message = message };

    public enum EnumRespType
    {
        None,
        Success,
        Error,
        ValidationError,
        SystemError,
        NotFound,
        DuplicateRecord,
        InvalidData,
        BadRequest
    }
}
