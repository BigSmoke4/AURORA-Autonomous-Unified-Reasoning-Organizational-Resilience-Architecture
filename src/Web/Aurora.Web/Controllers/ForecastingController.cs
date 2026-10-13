using System.Globalization;
using System.Text.Json;
using Aurora.Modules.Analysis.Contracts;
using Aurora.Modules.Analysis.Domain;
using Aurora.Modules.Audit.Application;
using Aurora.Modules.Audit.Domain;
using Aurora.Modules.Forecasting.Application;
using Aurora.Modules.Forecasting.Domain;
using Aurora.Modules.Identity.Domain;
using Aurora.Web.Models;
using Aurora.Web.Models.Forecasting;
using Aurora.Web.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

/// <summary>Holt linear-trend forecasting over a user-supplied series (spec section 17).</summary>
[Authorize(Policy = AuroraPermissions.ForecastRun)]
public class ForecastingController : Controller
{
    private static readonly char[] SeriesSeparators = { ',', ';', ' ', '\t', '\r', '\n' };

    private readonly CurrentOrganizationAccessor _orgAccessor;
    private readonly IAnalysisRecordRepository _analyses;
    private readonly AuditRecorder _audit;

    public ForecastingController(CurrentOrganizationAccessor orgAccessor, IAnalysisRecordRepository analyses, AuditRecorder audit)
    {
        _orgAccessor = orgAccessor;
        _analyses = analyses;
        _audit = audit;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return View("NoOrganization");

        return View(new ForecastForm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(ForecastForm form, [FromServices] IValidator<ForecastForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct);
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        form.ConfidenceOptions = HoltForecaster.SupportedConfidenceLevels;
        if (!ModelState.IsValid) return View("Index", form);

        var series = ParseSeries(form.SeriesText, out var parseError);
        if (parseError is not null)
        {
            ModelState.AddModelError(nameof(ForecastForm.SeriesText), parseError);
            return View("Index", form);
        }

        ForecastResult result;
        try
        {
            result = HoltForecaster.Forecast(series, form.Horizon, form.ConfidenceLevel);
        }
        catch (InvalidForecastRequestException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Index", form);
        }

        var userId = _orgAccessor.GetCurrentUserId() ?? Guid.Empty;
        var input = new { Series = series, form.Horizon, form.ConfidenceLevel };
        var title = $"Forecast: {form.Horizon} period(s) at {form.ConfidenceLevel:P0} confidence";
        var record = new AnalysisRecord(org.Id, AnalysisKind.Forecast, title, result.ModelVersion,
            JsonSerializer.Serialize(input), JsonSerializer.Serialize(result), userId);
        _analyses.Add(record);
        _audit.Record(org.Id, userId, User.ActorName(), AuditActions.ForecastRun, nameof(AnalysisRecord),
            record.Id.ToString(), AuditOutcome.Success, $"Model {result.ModelVersion}, {series.Count} observations.");
        await _analyses.SaveChangesAsync(ct);

        return RedirectToAction("Details", "Analysis", new { id = record.Id });
    }

    private static IReadOnlyList<decimal> ParseSeries(string text, out string? error)
    {
        error = null;
        var tokens = text.Split(SeriesSeparators, StringSplitOptions.RemoveEmptyEntries);
        var values = new List<decimal>(tokens.Length);
        foreach (var token in tokens)
        {
            if (!decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                error = $"'{token}' is not a number. Separate values with commas or spaces.";
                return Array.Empty<decimal>();
            }
            values.Add(value);
        }
        if (values.Count < HoltForecaster.MinimumObservations)
        {
            error = $"Enter at least {HoltForecaster.MinimumObservations} observations.";
        }
        else if (values.Count > HoltForecaster.MaximumObservations)
        {
            error = $"Enter at most {HoltForecaster.MaximumObservations} observations.";
        }
        return values;
    }
}
