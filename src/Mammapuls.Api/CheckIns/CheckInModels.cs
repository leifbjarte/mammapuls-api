using System.ComponentModel;
using Mammapuls.Api.Validation;

namespace Mammapuls.Api.CheckIns;

public enum StrengthSessions
{
    [Description("0")]
    Zero,
    [Description("1")]
    One,
    [Description("2")]
    Two,
    [Description("3")]
    Three,
    [Description("Flere enn 3")]
    MoreThanThree,
}

public enum IntervalSessions
{
    [Description("0")]
    Zero,
    [Description("1")]
    One,
    [Description("2")]
    Two,
    [Description("Flere enn 2")]
    MoreThanTwo,
}

public sealed record CheckInRequest : IBodyMeasurements
{
    [Description("Navn")]
    public string? FullName { get; init; }
    [Description("Vekt i kilo")]
    public decimal? WeightKg { get; init; }
    [Description("Midjemål i centimeter")]
    public decimal? WaistCm { get; init; }
    [Description("Hoftemål i centimeter")]
    public decimal? HipCm { get; init; }
    [Description("Brystmål i centimeter")]
    public decimal? ChestCm { get; init; }
    [Description("Gjennomsnittlig antall skritt per dag")]
    public int? AverageStepsPerDay { get; init; }
    [Description("Gjennomsnittlig antall kcal per dag")]
    public int? AverageKcalPerDay { get; init; }
    [Description("Antall dager (0-7) anbefalt proteininntak ble nådd")]
    public int? ProteinTargetDays { get; init; }
    [Description("Hva som gjorde proteininntaket utfordrende (valgfritt)")]
    public string? ProteinChallenge { get; init; }
    [Description("Antall dager (0-7) anbefalt kaloriinntak ble fulgt")]
    public int? CalorieTargetDays { get; init; }
    [Description("Hva som gjorde kaloriinntaket utfordrende (valgfritt)")]
    public string? CalorieChallenge { get; init; }
    [Description("Antall gjennomførte styrkeøkter")]
    public StrengthSessions? StrengthSessions { get; init; }
    [Description("Antall gjennomførte intervalløkter")]
    public IntervalSessions? IntervalSessions { get; init; }
    [Description("Stressnivå denne uken (1-10, 10 er høyest)")]
    public int? StressLevel { get; init; }
    [Description("Søvnkvalitet denne uken (1-10, 10 er best)")]
    public int? SleepQuality { get; init; }
    [Description("Hva fungerte bra denne uken")]
    public string? WhatWorkedWell { get; init; }
    [Description("Hva var utfordrende")]
    public string? WhatWasChallenging { get; init; }
    [Description("Trenger du hjelp med noe")]
    public string? NeedHelpWith { get; init; }
}

public sealed record CheckInSubmission(
    string Id,
    string UserId,
    string Week,
    string FullName,
    decimal WeightKg,
    decimal WaistCm,
    decimal HipCm,
    decimal ChestCm,
    int AverageStepsPerDay,
    int AverageKcalPerDay,
    int ProteinTargetDays,
    string? ProteinChallenge,
    int CalorieTargetDays,
    string? CalorieChallenge,
    StrengthSessions StrengthSessions,
    IntervalSessions IntervalSessions,
    int StressLevel,
    int SleepQuality,
    string WhatWorkedWell,
    string WhatWasChallenging,
    string NeedHelpWith,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CheckInResponse(
    string Week,
    string FullName,
    decimal WeightKg,
    decimal WaistCm,
    decimal HipCm,
    decimal ChestCm,
    int AverageStepsPerDay,
    int AverageKcalPerDay,
    int ProteinTargetDays,
    string? ProteinChallenge,
    int CalorieTargetDays,
    string? CalorieChallenge,
    StrengthSessions StrengthSessions,
    IntervalSessions IntervalSessions,
    int StressLevel,
    int SleepQuality,
    string WhatWorkedWell,
    string WhatWasChallenging,
    string NeedHelpWith,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static CheckInResponse From(CheckInSubmission s) => new(
        s.Week, s.FullName, s.WeightKg, s.WaistCm, s.HipCm, s.ChestCm, s.AverageStepsPerDay, s.AverageKcalPerDay,
        s.ProteinTargetDays, s.ProteinChallenge, s.CalorieTargetDays, s.CalorieChallenge, s.StrengthSessions,
        s.IntervalSessions, s.StressLevel, s.SleepQuality, s.WhatWorkedWell, s.WhatWasChallenging, s.NeedHelpWith,
        s.CreatedAt, s.UpdatedAt);
}
