namespace Aurora.Web.Models.Modules;

public sealed record AlertRow(
    Guid Id,
    string Title,
    string? Detail,
    string Severity,
    string Status,
    string Tone,
    DateTime RaisedAtUtc,
    DateTime? AcknowledgedAtUtc,
    bool CanAcknowledge,
    bool CanResolve);

public sealed record AlertsIndexViewModel(IReadOnlyList<AlertRow> Rows, bool CanManage);
