using System.ComponentModel;

namespace Mammapuls.Api.Onboarding;

public enum SleepPerNight
{
    [Description("Under 5 timer")]
    UnderFiveHours,
    [Description("5-6 timer")]
    FiveToSixHours,
    [Description("6-7 timer")]
    SixToSevenHours,
    [Description("7-8 timer")]
    SevenToEightHours,
    [Description("Over 8 timer")]
    OverEightHours,
}

public enum StepsPerDay
{
    [Description("Under 5 000 skritt")]
    Under5000,
    [Description("5 000-10 000 skritt")]
    From5000To10000,
    [Description("Over 10 000 skritt")]
    Over10000,
}

public enum TrainingDaysPerWeek
{
    [Description("Ingen treningsdager")]
    None,
    [Description("1-2 dager")]
    OneToTwo,
    [Description("3-4 dager")]
    ThreeToFour,
    [Description("5 eller flere dager")]
    FiveOrMore,
}

public enum FoodAndExerciseRelationship
{
    [Description("God")]
    Good,
    [Description("Greit")]
    Okay,
    [Description("Vanskelig")]
    Difficult,
    [Description("Svært vanskelig")]
    VeryDifficult,
}

public sealed record OnboardingRequest
{
    [Description("Fullt navn")]
    public string? FullName { get; init; }
    [Description("Alder")]
    public int? Age { get; init; }
    [Description("Timer søvn per natt")]
    public SleepPerNight? SleepPerNight { get; init; }
    [Description("Stressnivå")]
    public int? StressLevel { get; init; }
    [Description("Vekt i kilo")]
    public decimal? WeightKg { get; init; }
    [Description("Høyde i centimeter")]
    public decimal? HeightCm { get; init; }
    [Description("Midjemål i centimeter")]
    public decimal? WaistCm { get; init; }
    [Description("Brystmål i centimeter")]
    public decimal? ChestCm { get; init; }
    [Description("Hoftemål i centimeter")]
    public decimal? HipCm { get; init; }
    [Description("Kan jogge komfortabelt")]
    public bool? CanJogComfortably { get; init; }
    [Description("Skritt per dag")]
    public StepsPerDay? StepsPerDay { get; init; }
    [Description("Treningsdager per uke")]
    public TrainingDaysPerWeek? TrainingDaysPerWeek { get; init; }
    [Description("Er vegetarianer")]
    public bool? IsVegetarian { get; init; }
    [Description("Er pescetarianer")]
    public bool? IsPescetarian { get; init; }
    [Description("Amming")]
    public bool? IsBreastfeeding { get; init; }
    [Description("Har allergier")]
    public bool? HasAllergies { get; init; }
    [Description("Allergidetaljer")]
    public string? AllergyDetails { get; init; }
    [Description("Har skader")]
    public bool? HasInjuries { get; init; }
    [Description("Skadedetaljer")]
    public string? InjuryDetails { get; init; }
    [Description("Har prøvd vektnedgang tidligere")]
    public bool? HasTriedWeightLossBefore { get; init; }
    [Description("Erfaring med tidligere vektnedgang")]
    public string? PreviousWeightLossExperience { get; init; }
    [Description("Forhold til mat og trening")]
    public FoodAndExerciseRelationship? FoodAndExerciseRelationship { get; init; }
    [Description("Har hatt en spiseforstyrrelse")]
    public bool? HasHadEatingDisorder { get; init; }
    [Description("Tilfredshet med egen helse")]
    public int? HealthSatisfaction { get; init; }
    [Description("Motivasjon for programmet")]
    public int? ProgramMotivation { get; init; }
    [Description("Mål etter åtte uker")]
    public string? GoalAfterEightWeeks { get; init; }
    [Description("Største utfordring")]
    public string? BiggestChallenge { get; init; }
    [Description("Annet coachen bør vite")]
    public string? AnythingElseForCoach { get; init; }
}

public sealed record OnboardingSubmission(
    string Id,
    string UserId,
    string FullName,
    int Age,
    SleepPerNight SleepPerNight,
    int StressLevel,
    decimal WeightKg,
    decimal HeightCm,
    decimal WaistCm,
    decimal ChestCm,
    decimal HipCm,
    bool CanJogComfortably,
    StepsPerDay StepsPerDay,
    TrainingDaysPerWeek TrainingDaysPerWeek,
    bool IsVegetarian,
    bool IsPescetarian,
    bool IsBreastfeeding,
    bool HasAllergies,
    string? AllergyDetails,
    bool HasInjuries,
    string? InjuryDetails,
    bool HasTriedWeightLossBefore,
    string? PreviousWeightLossExperience,
    FoodAndExerciseRelationship FoodAndExerciseRelationship,
    bool HasHadEatingDisorder,
    int HealthSatisfaction,
    int ProgramMotivation,
    string GoalAfterEightWeeks,
    string BiggestChallenge,
    string AnythingElseForCoach,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record OnboardingResponse(
    string FullName,
    int Age,
    SleepPerNight SleepPerNight,
    int StressLevel,
    decimal WeightKg,
    decimal HeightCm,
    decimal WaistCm,
    decimal ChestCm,
    decimal HipCm,
    bool CanJogComfortably,
    StepsPerDay StepsPerDay,
    TrainingDaysPerWeek TrainingDaysPerWeek,
    bool IsVegetarian,
    bool IsPescetarian,
    bool IsBreastfeeding,
    bool HasAllergies,
    string? AllergyDetails,
    bool HasInjuries,
    string? InjuryDetails,
    bool HasTriedWeightLossBefore,
    string? PreviousWeightLossExperience,
    FoodAndExerciseRelationship FoodAndExerciseRelationship,
    bool HasHadEatingDisorder,
    int HealthSatisfaction,
    int ProgramMotivation,
    string GoalAfterEightWeeks,
    string BiggestChallenge,
    string AnythingElseForCoach,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static OnboardingResponse From(OnboardingSubmission submission) => new(
        submission.FullName,
        submission.Age,
        submission.SleepPerNight,
        submission.StressLevel,
        submission.WeightKg,
        submission.HeightCm,
        submission.WaistCm,
        submission.ChestCm,
        submission.HipCm,
        submission.CanJogComfortably,
        submission.StepsPerDay,
        submission.TrainingDaysPerWeek,
        submission.IsVegetarian,
        submission.IsPescetarian,
        submission.IsBreastfeeding,
        submission.HasAllergies,
        submission.AllergyDetails,
        submission.HasInjuries,
        submission.InjuryDetails,
        submission.HasTriedWeightLossBefore,
        submission.PreviousWeightLossExperience,
        submission.FoodAndExerciseRelationship,
        submission.HasHadEatingDisorder,
        submission.HealthSatisfaction,
        submission.ProgramMotivation,
        submission.GoalAfterEightWeeks,
        submission.BiggestChallenge,
        submission.AnythingElseForCoach,
        submission.CreatedAt,
        submission.UpdatedAt);
}