using System;
using System.Collections.Generic;

namespace CRM.Shared.DTOs
{
    public class DealForecastFilter
    {
        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        public string? IdUser { get; set; }
    }

    public class CommercialForecastDTO
    {
        public DateTime DateFrom { get; set; }

        public DateTime DateTo { get; set; }

        public int DealCount { get; set; }

        public decimal OpenPipeline { get; set; }

        public decimal WeightedPipeline { get; set; }

        public decimal WonAmount { get; set; }

        public decimal LostAmount { get; set; }

        public decimal TargetAmount { get; set; }

        public decimal CoveragePct => TargetAmount == 0 ? 0 : Math.Round(WonAmount / TargetAmount * 100, 1);

        public List<CommercialForecastBucketDTO> ByMonth { get; set; } = new();

        public List<CommercialForecastBucketDTO> ByOwner { get; set; } = new();

        public List<CommercialForecastBucketDTO> ByPhase { get; set; } = new();
    }

    public class CommercialForecastBucketDTO
    {
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public int DealCount { get; set; }

        public decimal Amount { get; set; }

        public decimal WeightedAmount { get; set; }

        public decimal WonAmount { get; set; }

        public decimal TargetAmount { get; set; }
    }
}
