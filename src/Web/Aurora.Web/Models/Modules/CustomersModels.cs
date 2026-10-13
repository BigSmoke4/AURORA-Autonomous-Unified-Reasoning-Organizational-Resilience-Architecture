namespace Aurora.Web.Models.Modules;

public sealed record CustomerRow(string Name, decimal Exposure, decimal SharePct);

public sealed record CustomersIndexViewModel(
    decimal TotalExposure,
    decimal TopCustomerSharePct,
    decimal TopThreeSharePct,
    decimal Hhi,
    string Band,
    string Tone,
    IReadOnlyList<CustomerRow> Rows);
