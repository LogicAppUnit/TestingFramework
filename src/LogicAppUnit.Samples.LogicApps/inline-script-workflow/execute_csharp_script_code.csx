// Add the required libraries
#r "Newtonsoft.Json"
#r "Microsoft.Azure.Workflows.Scripting"
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Workflows.Scripting;
using Newtonsoft.Json.Linq;

/// <summary>
/// Executes the inline csharp code.
/// </summary>
/// <param name="context">The workflow context.</param>
/// <remarks> This is the entry-point to your code. The function signature should remain unchanged.</remarks>
public static async Task<Results> Run(WorkflowContext context, ILogger log)
{
  var triggerOutputs = (await context.GetTriggerResults().ConfigureAwait(false)).Outputs;

  ////the following dereferences the 'name' property from trigger payload.
  var name = triggerOutputs?["body"]?["name"]?.ToString();

  ////the following can be used to get the action outputs from a prior action
  //var actionOutputs = (await context.GetActionResults("Compose").ConfigureAwait(false)).Outputs;

  ////these logs will show-up in Application Insight traces table
  //log.LogInformation("Outputting results.");

  //var name = null;

  return new Results
  {
    Message = !string.IsNullOrEmpty(name) ? $"Hello {name} from CSharp action" : "Hello from CSharp action."
  };
}

public class Results
{
  public string Message {get; set;}
}