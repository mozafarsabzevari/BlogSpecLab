namespace BlogSpecLab.Core.Domain.ContentReviews;

public sealed class MissingInformationItem
{
    public const int MaxDescriptionLength = 1000;

    private MissingInformationItem()
    {
    }

    public MissingInformationItem(string description)
    {
        if (string.IsNullOrWhiteSpace(description) || description.Length > MaxDescriptionLength)
        {
            throw new ContentReviewDomainException(
                ContentReviewErrorCode.InvalidMissingInformation,
                $"توضیح اطلاعات ناکافی الزامی است و حداکثر {MaxDescriptionLength} نویسه دارد.");
        }

        Description = description;
    }

    public string Description { get; private set; } = string.Empty;
}
