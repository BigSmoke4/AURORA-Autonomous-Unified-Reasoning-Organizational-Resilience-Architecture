using FluentValidation;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Aurora.Web.Validation;

/// <summary>
/// Bridges FluentValidation to MVC. ASP.NET Core's automatic validation pipeline is deprecated for FluentValidation,
/// so each POST action runs its validator explicitly and the failures go to ModelState for the view to show.
/// </summary>
public static class FormValidationExtensions
{
    public static async Task ValidateIntoModelStateAsync<T>(
        this IValidator<T> validator, T model, ModelStateDictionary modelState, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(model, ct);
        foreach (var failure in result.Errors)
        {
            modelState.AddModelError(failure.PropertyName ?? string.Empty, failure.ErrorMessage);
        }
    }
}

/// <summary>Numeric limits that the form validators share. Percentages are expressed in points from 0 to 100.</summary>
public static class ValidationLimits
{
    public const int MinimumPercentPoints = 0;
    public const int MaximumPercentPoints = 100;
    public const decimal MinimumPercent = 0m;
    public const decimal MaximumPercent = 100m;
    public const int MaximumEmailLength = 256;
}
