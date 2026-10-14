namespace Aurora.Web.Models.Modules;

public sealed record CustomerRow(Guid CustomerId, string Name, decimal Exposure, decimal SharePct);

public sealed record CustomerDetailsViewModel(
    Guid CustomerId,
    string Name,
    decimal Exposure,
    decimal SharePct,
    int Rank,
    int CustomerCount,
    decimal Hhi,
    decimal HhiWithoutCustomer,
    string BandWithoutCustomer,
    string Tone);

public sealed record CustomersIndexViewModel(
    decimal TotalExposure,
    decimal TopCustomerSharePct,
    decimal TopThreeSharePct,
    decimal Hhi,
    string Band,
    string Tone,
    IReadOnlyList<CustomerRow> Rows);
