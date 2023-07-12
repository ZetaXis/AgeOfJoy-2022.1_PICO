using System;
using System.Collections.Generic;
using System.IO;

class CommandEND : ICommandBase
{
    public string CmdToken { get; } = "END";
    public CommandType.Type Type { get; } = CommandType.Type.Command;

    public CommandEND()
    {
    }
    public bool Parse(TokenConsumer tokens)
    {
        return true;
    }

    public BasicValue Execute(BasicVars vars)
    {
        ConfigManager.WriteConsole($"[AGE BASIC RUN {CmdToken}]");

        //don't change the BasicValue var.
        vars.GetValue("_linenumber").SetValue(double.MaxValue);

        return null;
    }

}