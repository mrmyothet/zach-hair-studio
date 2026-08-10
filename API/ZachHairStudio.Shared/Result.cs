using ZachHairStudio.Shared.Features.Availability;

namespace ZachHairStudio.Shared;

public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public bool IsError => !IsSuccess;

    private EnumRespType Type { get; set; }
    public T Data { get; private set; } = default!;
    public string Message { get; private set; } = string.Empty;

    // Side-channel for the availability conflict-check outcome (MGMT-03, D-09).
    // Deliberately independent of T: a ConflictError on, say,
    // Result<StylistTimeOff> still needs to carry an
    // IReadOnlyList<AvailabilityConflictDto> even though T is StylistTimeOff,
    // not the conflict list — the generic Data field can't hold both shapes.
    // Only ever populated by ConflictError; every other factory leaves it null.
    public IReadOnlyList<AvailabilityConflictDto>? Conflicts { get; private set; }

    public bool IsValidationError() => Type == EnumRespType.ValidationError;
    public bool IsSystemError() => Type == EnumRespType.SystemError;
    public bool IsDataError() => Type == EnumRespType.Error;
    public bool IsNotFound() => Type == EnumRespType.NotFound;
    public bool IsDuplicateRecord() => Type == EnumRespType.DuplicateRecord;
    public bool IsInvalidData() => Type == EnumRespType.InvalidData;
    public bool IsBadRequest() => Type == EnumRespType.BadRequest;
    public bool IsConflict() => Type == EnumRespType.Conflict;

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

    /// <summary>
    /// One or more Confirmed appointments would fall outside the newly proposed
    /// availability (MGMT-03, D-09). The write did NOT persist — see
    /// <see cref="Conflicts"/> for the actionable, D-11-scoped conflict list.
    /// </summary>
    public static Result<T> ConflictError(
        string message,
        IReadOnlyList<AvailabilityConflictDto> conflicts,
        T? data = default) =>
        new Result<T>
        {
            IsSuccess = false,
            Type = EnumRespType.Conflict,
            Data = data,
            Message = message,
            Conflicts = conflicts,
        };

    /// <summary>
    /// Message-only conflict (e.g. insufficient stock at checkout). Keeps
    /// <see cref="IsConflict"/> true with <see cref="Conflicts"/> left null —
    /// Pitfall 7 / SHOP-04 path.
    /// </summary>
    public static Result<T> ConflictError(string message, T? data = default) =>
        new Result<T>
        {
            IsSuccess = false,
            Type = EnumRespType.Conflict,
            Data = data,
            Message = message,
            Conflicts = null,
        };

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
        BadRequest,
        Conflict
    }
}
