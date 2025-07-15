namespace AviUtlPluginNet.PreBuiltTests;

public class SkipEmptyEnvironmentVariableAttribute : FactAttribute
{
    public SkipEmptyEnvironmentVariableAttribute(string environmentVariable)
    {
        var actualValue = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrEmpty(actualValue))
        {
            Skip = $"Test skipped because {environmentVariable} is not set or empty.";
        }
    }
}
