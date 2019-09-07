var target = Argument("target", "Default");
var projectName = "AngleSharp.Io";
var solutionName = "AngleSharp.Io";
var frameworks = new Dictionary<String, String>
{
    { "netstandard2.0", "netstandard2.0" },
};

#load tools/anglesharp.cake

RunTarget(target);
