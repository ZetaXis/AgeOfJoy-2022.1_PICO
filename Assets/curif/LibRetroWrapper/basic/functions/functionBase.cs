using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

class CommandFunctionBase : ICommandBase
{
    protected string cmdToken = "UNKNOWN";
    public string CmdToken { get { return cmdToken; } }
    public CommandType.Type Type { get; } = CommandType.Type.Function;

    protected ConfigurationCommands config;

    public CommandFunctionBase(ConfigurationCommands config)
    {
        this.config = config;
    }

    public virtual bool Parse(TokenConsumer tokens)
    {
        throw new Exception("function without parse method");
    }

    public virtual BasicValue Execute(BasicVars vars)
    {
        throw new Exception("function without execution method");
    }

    public override string ToString()
    {
        return "Func: " + CmdToken;
    }

}

class CommandFunctionExpressionListBase : CommandFunctionBase
{
    protected CommandExpressionList exprs;

    public CommandFunctionExpressionListBase(ConfigurationCommands config) : base(config)
    {
        exprs = new(config);
    }

    public override bool Parse(TokenConsumer tokens)
    {
        // FNCT ( expr ,  ... )
        // tokens points to FNCT

        ConfigManager.WriteConsole($"[functionBase.Parse] START  {tokens.ToString()}");

        if (tokens.Next() != "(")
            throw new Exception($"function without enclosing (): {tokens.ToString()}");
        tokens++; //consumes (

        ConfigManager.WriteConsole($"[FunctionBase.Parse] EXPR LIST {tokens.ToString()}");
        exprs.Parse(tokens);

        if (tokens.Token != ")")
            throw new Exception($"function without enclosing () END is missing: {tokens.ToString()}");
        //tokens++; //consumes )
        //always ends in the final token.

        ConfigManager.WriteConsole($"[functionBase.Parse] END {tokens.ToString()}");
        return true;
    }
}

class CommandFunctionSingleExpressionBase : CommandFunctionBase
{
    protected CommandExpression expr;

    public CommandFunctionSingleExpressionBase(ConfigurationCommands config) : base(config)
    {
        expr = new(config);
    }

    public override bool Parse(TokenConsumer tokens)
    {
        // FNCT ( expr ,  ... )
        // tokens points to FNCT

        ConfigManager.WriteConsole($"[CommandFunctionSingleExpressionBase.Parse] START  {tokens.ToString()}");

        if (tokens.Next() != "(")
            throw new Exception($"function without enclosing (): {tokens.ToString()}");
        tokens++; //consumes (

        ConfigManager.WriteConsole($"[CommandFunctionSingleExpressionBase.Parse] EXPR LIST {tokens.ToString()}");
        expr.Parse(tokens);

        if (tokens.Token != ")")
            throw new Exception($"function without enclosing () END is missing: {tokens.ToString()}");
        //tokens++; //consumes )
        //always ends in the final token.

        ConfigManager.WriteConsole($"[CommandFunctionSingleExpressionBase.Parse] END {tokens.ToString()}");
        return true;
    }
}