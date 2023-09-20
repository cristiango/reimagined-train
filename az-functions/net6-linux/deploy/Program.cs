// See https://aka.ms/new-console-template for more information

using static Bullseye.Targets;

const string Up = "Up";
const string Preview = "Preview";
var bullseyeArgs = args.Where(x => x == "--help" || !x.Contains("--"));

Target(Preview, () =>
{
    
});

Target(Up, () =>
{
    
});

RunTargetsAndExitAsync(bullseyeArgs);