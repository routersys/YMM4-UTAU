using System.ComponentModel.DataAnnotations;

namespace UTAU.Synthesis;

public enum F0Estimator
{
    [Display(Name = nameof(Texts.F0EstimatorHarvest), Description = nameof(Texts.F0EstimatorHarvestDescription), ResourceType = typeof(Texts))]
    Harvest,

    [Display(Name = nameof(Texts.F0EstimatorDio), Description = nameof(Texts.F0EstimatorDioDescription), ResourceType = typeof(Texts))]
    Dio,
}
