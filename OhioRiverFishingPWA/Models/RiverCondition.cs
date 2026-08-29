using System.Text.Json.Serialization;

namespace OhioRiverFishingPWA.Models;

public class RiverMetrics
{
    public double GaugeHeightFeet { get; set; }
    public double FlowRateCFS { get; set; }
    public string FloodCategory { get; set; } = "no_flooding";
    public string StageTrend { get; set; } = "Stable";
    public DateTime Timestamp { get; set; }
}

public class SolunarWindow
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Category { get; set; } = string.Empty;
    public double RatingScore { get; set; }
}

public class TargetFishRecommendation
{
    public string Species { get; set; } = string.Empty;
    public string ActivityLevel { get; set; } = string.Empty;
    public string PrimaryBait { get; set; } = string.Empty;
    public string TacticalRig { get; set; } = string.Empty;
    public string DepthStrategy { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}

public class StageForecastPoint
{
    public DateTime ValidTime { get; set; }
    public double StageFeet { get; set; }
    public bool IsForecast { get; set; }
}

public class LockDamInfo
{
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public double RiverMile { get; set; }
    public string Direction { get; set; } = string.Empty;
    public double MilesFromWheelersburg { get; set; }
    public string State { get; set; } = string.Empty;
    public string PoolName { get; set; } = string.Empty;
    public string UsaceDistrict { get; set; } = string.Empty;
    public string ScheduleUrl { get; set; } = string.Empty;
    public string StatusNote { get; set; } = string.Empty;
}

// ── USGS JSON deserialization ────────────────────────────────────────────────

public class UsgsResponse
{
    public UsgsValue? Value { get; set; }
}

public class UsgsValue
{
    public List<UsgsTimeSeries> TimeSeries { get; set; } = new();
}

public class UsgsTimeSeries
{
    public UsgsVariable Variable { get; set; } = new();
    public List<UsgsValues> Values { get; set; } = new();
}

public class UsgsVariable
{
    public List<UsgsVariableCode> VariableCode { get; set; } = new();
    public string VariableName { get; set; } = string.Empty;
}

public class UsgsVariableCode
{
    public string Value { get; set; } = string.Empty;
}

public class UsgsValues
{
    public List<UsgsMeasurement> Value { get; set; } = new();
}

public class UsgsMeasurement
{
    public string Value { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }
}

// ── NOAA NWPS JSON deserialization ─────────────────────────────────────────

public class NoaaStageflowResponse
{
    public NoaaStageflowSeries Observed { get; set; } = new();
    public NoaaStageflowSeries Forecast { get; set; } = new();
}

public class NoaaStageflowSeries
{
    public string IssuedTime { get; set; } = string.Empty;
    public string PrimaryUnits { get; set; } = string.Empty;
    public List<NoaaDataPoint> Data { get; set; } = new();
}

public class NoaaDataPoint
{
    public DateTime ValidTime { get; set; }
    public double Primary { get; set; }
    public double Secondary { get; set; }
}

// ── NOAA gauge status (current observed) ─────────────────────────────────────

public class NoaaGaugeResponse
{
    public NoaaGaugeStatus? Status { get; set; }
}

public class NoaaGaugeStatus
{
    public NoaaGaugeObserved? Observed { get; set; }
    public NoaaGaugeObserved? Forecast { get; set; }
}

public class NoaaGaugeObserved
{
    public double Primary { get; set; }
    public string PrimaryUnit { get; set; } = string.Empty;
    public double Secondary { get; set; }
    public string SecondaryUnit { get; set; } = string.Empty;
    public string FloodCategory { get; set; } = string.Empty;
    public DateTime ValidTime { get; set; }
}
