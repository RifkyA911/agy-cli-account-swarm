namespace AgyAccountSwarm.Models;

public class ChartDataPoint
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
    public double Height { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public string TokensLabel { get; set; } = string.Empty;
    public string TooltipText { get; set; } = string.Empty;
    public string BarColor { get; set; } = "#3B82F6";
    public string TimeRange { get; set; } = string.Empty;
    public string ModelContext { get; set; } = string.Empty;
    public string AccountContext { get; set; } = string.Empty;
    public double Width { get; set; } = 36.0;
}

public class DashboardLanguageOption
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public override string ToString() => DisplayName;
}
