namespace SpaceBattle.Lib;


public class MeteoritesPlugin : IMatchRulePlugin
{
    public string RuleName => "метеориты";
}


public class FuelLimitPlugin : IMatchRulePlugin
{
    public string RuleName => "ограниченный запас топлива";
}
