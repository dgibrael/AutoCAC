using System.Text.Json;

namespace AutoCAC.Models;

public enum ScheduledTaskHandlerKey
{
    GbReport
}

public partial class ScheduledTask
{
    public ScheduledTaskHandlerKey HandlerKeyEnum => Enum.Parse<ScheduledTaskHandlerKey>(HandlerKey, ignoreCase: true);
    public TParameters GetParameters<TParameters>() where TParameters : class, new()
    {
        if (string.IsNullOrWhiteSpace(ParametersJson))
        {
            return new TParameters();
        }

        return JsonSerializer.Deserialize<TParameters>(ParametersJson) ?? new TParameters();
    }
    public void SetParameters<TParameters>(TParameters parameters) where TParameters : class
    {
        ParametersJson = JsonSerializer.Serialize(parameters);
    }
}