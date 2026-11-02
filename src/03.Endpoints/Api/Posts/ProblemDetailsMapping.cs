using System.Text;
using BlogSpecLab.Core.Application.Common;

namespace BlogSpecLab.Endpoints.Api.Posts;

public static class ProblemDetailsMapping
{
    private const string TypeBaseUri = "https://blogspeclab/errors/";

    public static IResult ToProblem(this Error error)
    {
        var (status, title) = error.Code switch
        {
            ErrorCode.InvalidTitle or ErrorCode.InvalidBody or ErrorCode.ReturnReasonRequired
                => (StatusCodes.Status400BadRequest, "درخواست نامعتبر است."),
            ErrorCode.Unauthenticated
                => (StatusCodes.Status401Unauthorized, "ورود به سامانه لازم است."),
            ErrorCode.Forbidden or ErrorCode.SelfApprovalNotAllowed
                => (StatusCodes.Status403Forbidden, "اجازهٔ انجام این عملیات را ندارید."),
            ErrorCode.NotFound
                => (StatusCodes.Status404NotFound, "پست پیدا نشد."),
            ErrorCode.InvalidStatusTransition or ErrorCode.DeleteNotAllowed or ErrorCode.ConfirmationRequired
                => (StatusCodes.Status409Conflict, "وضعیت پست با این عملیات سازگار نیست."),
            ErrorCode.StaleVersion
                => (StatusCodes.Status412PreconditionFailed, "نسخهٔ پست تغییر کرده است."),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Code, "Unmapped error code.")
        };

        var extensions = new Dictionary<string, object?> { ["code"] = error.Code.ToString() };

        if (status is StatusCodes.Status409Conflict or StatusCodes.Status412PreconditionFailed)
        {
            if (error.CurrentStatus is not null)
            {
                extensions["currentStatus"] = error.CurrentStatus;
            }

            if (error.CurrentVersion is not null)
            {
                extensions["currentVersion"] = error.CurrentVersion;
            }
        }

        return Results.Problem(
            detail: error.Message,
            statusCode: status,
            title: title,
            type: TypeBaseUri + ToKebabCase(error.Code.ToString()),
            extensions: extensions);
    }

    public static IResult Unauthenticated() =>
        new Error(ErrorCode.Unauthenticated, "Token ارسال نشده، نامعتبر یا منقضی است، یا کاربر غیرفعال است.")
            .ToProblem();

    private static string ToKebabCase(string value)
    {
        var builder = new StringBuilder(value.Length + 8);

        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]) && i > 0)
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(value[i]));
        }

        return builder.ToString();
    }
}
