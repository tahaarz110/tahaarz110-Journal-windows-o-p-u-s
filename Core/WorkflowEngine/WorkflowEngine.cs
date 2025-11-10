// ابتدای فایل: Core/WorkflowEngine/WorkflowEngine.cs
// مسیر: /Core/WorkflowEngine/WorkflowEngine.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Serilog;
using TradingJournal.Core.RuleEngine;
using TradingJournal.Data;

namespace TradingJournal.Core.WorkflowEngine
{
    public enum WorkflowStatus
    {
        Created,
        Running,
        Paused,
        Completed,
        Failed,
        Cancelled
    }

    public enum StepType
    {
        Start,
        End,
        Action,
        Decision,
        Loop,
        Parallel,
        Wait,
        SubWorkflow
    }

    public class WorkflowDefinition
    {
        public string WorkflowId { get; set; } = Guid.NewGuid().ToString();
        public string WorkflowName { get; set; } = string.Empty;
        public string WorkflowNameFa { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Trigger { get; set; } = string.Empty; // Event that triggers workflow
        public List<WorkflowStep> Steps { get; set; } = new();
        public Dictionary<string, object> Variables { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }

    public class WorkflowStep
    {
        public string StepId { get; set; } = Guid.NewGuid().ToString();
        public string StepName { get; set; } = string.Empty;
        public StepType Type { get; set; }
        public string? Condition { get; set; } // Expression for decision steps
        public Dictionary<string, object> Parameters { get; set; } = new();
        public List<string> NextSteps { get; set; } = new(); // For branching
        public string? ErrorHandler { get; set; } // Step to go on error
        public int? MaxRetries { get; set; }
        public int? TimeoutSeconds { get; set; }
    }

    public class WorkflowInstance
    {
        public string InstanceId { get; set; } = Guid.NewGuid().ToString();
        public string WorkflowId { get; set; } = string.Empty;
        public WorkflowStatus Status { get; set; } = WorkflowStatus.Created;
        public string? CurrentStepId { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
        public List<WorkflowLog> Logs { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class WorkflowLog
    {
        public string StepId { get; set; } = string.Empty;
        public string StepName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Message { get; set; } = string.Empty;
        public bool IsError { get; set; }
        public Dictionary<string, object>? Data { get; set; }
    }

    public class WorkflowEngine
    {
        private readonly Dictionary<string, WorkflowDefinition> _workflows;
        private readonly Dictionary<string, WorkflowInstance> _instances;
        private readonly Dictionary<string, Func<WorkflowStep, WorkflowInstance, Task<StepResult>>> _stepHandlers;
        private readonly RuleEngine.RuleEngine _ruleEngine;
        private readonly DatabaseContext _dbContext;

        public WorkflowEngine()
        {
            _workflows = new Dictionary<string, WorkflowDefinition>();
            _instances = new Dictionary<string, WorkflowInstance>();
            _stepHandlers = new Dictionary<string, Func<WorkflowStep, WorkflowInstance, Task<StepResult>>>();
            _ruleEngine = new RuleEngine.RuleEngine();
            _dbContext = new DatabaseContext();
            
            RegisterDefaultHandlers();
            LoadWorkflowDefinitions();
        }

        private void RegisterDefaultHandlers()
        {
            // Register built-in action handlers
            RegisterStepHandler("log", LogHandler);
            RegisterStepHandler("save", SaveHandler);
            RegisterStepHandler("email", EmailHandler);
            RegisterStepHandler("calculate", CalculateHandler);
            RegisterStepHandler("validate", ValidateHandler);
            RegisterStepHandler("transform", TransformHandler);
            RegisterStepHandler("query", QueryHandler);
            RegisterStepHandler("update", UpdateHandler);
            RegisterStepHandler("delete", DeleteHandler);
            RegisterStepHandler("notify", NotifyHandler);
        }

        private void LoadWorkflowDefinitions()
        {
            try
            {
                var workflowsPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TradingJournal",
                    "Metadata",
                    "workflows"
                );

                if (System.IO.Directory.Exists(workflowsPath))
                {
                    var files = System.IO.Directory.GetFiles(workflowsPath, "*.json");
                    foreach (var file in files)
                    {
                        var json = System.IO.File.ReadAllText(file);
                        var workflowData = JObject.Parse(json);
                        var workflow = ParseWorkflowDefinition(workflowData);
                        RegisterWorkflow(workflow);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading workflow definitions");
            }
        }

        private WorkflowDefinition ParseWorkflowDefinition(JObject data)
        {
            var workflow = new WorkflowDefinition
            {
                WorkflowId = data["workflowId"]?.ToString() ?? Guid.NewGuid().ToString(),
                WorkflowName = data["workflowName"]?.ToString() ?? "",
                WorkflowNameFa = data["workflowNameFa"]?.ToString() ?? "",
                Description = data["description"]?.ToString() ?? "",
                Trigger = data["trigger"]?.ToString() ?? "",
                IsActive = data["isActive"]?.Value<bool>() ?? true
            };

            // Parse steps
            var steps = data["steps"] as JArray;
            if (steps != null)
            {
                foreach (JObject stepData in steps)
                {
                    workflow.Steps.Add(ParseWorkflowStep(stepData));
                }
            }

            // Parse variables
            var variables = data["variables"] as JObject;
            if (variables != null)
            {
                workflow.Variables = variables.ToObject<Dictionary<string, object>>() ?? new();
            }

            return workflow;
        }

        private WorkflowStep ParseWorkflowStep(JObject data)
        {
            return new WorkflowStep
            {
                StepId = data["stepId"]?.ToString() ?? Guid.NewGuid().ToString(),
                StepName = data["stepName"]?.ToString() ?? "",
                Type = Enum.Parse<StepType>(data["type"]?.ToString() ?? "Action"),
                Condition = data["condition"]?.ToString(),
                Parameters = data["parameters"]?.ToObject<Dictionary<string, object>>() ?? new(),
                NextSteps = data["nextSteps"]?.ToObject<List<string>>() ?? new(),
                ErrorHandler = data["errorHandler"]?.ToString(),
                MaxRetries = data["maxRetries"]?.Value<int>(),
                TimeoutSeconds = data["timeoutSeconds"]?.Value<int>()
            };
        }

        public void RegisterWorkflow(WorkflowDefinition workflow)
        {
            _workflows[workflow.WorkflowId] = workflow;
            Log.Information($"Registered workflow: {workflow.WorkflowName}");
        }

        public void RegisterStepHandler(string actionType, Func<WorkflowStep, WorkflowInstance, Task<StepResult>> handler)
        {
            _stepHandlers[actionType.ToLower()] = handler;
        }

        public async Task<string> StartWorkflowAsync(string workflowId, Dictionary<string, object>? initialContext = null)
        {
            if (!_workflows.TryGetValue(workflowId, out var workflow))
            {
                throw new InvalidOperationException($"Workflow {workflowId} not found");
            }

            var instance = new WorkflowInstance
            {
                WorkflowId = workflowId,
                Status = WorkflowStatus.Running,
                StartTime = DateTime.Now,
                Context = initialContext ?? new Dictionary<string, object>()
            };

            // Add workflow variables to context
            foreach (var variable in workflow.Variables)
            {
                if (!instance.Context.ContainsKey(variable.Key))
                {
                    instance.Context[variable.Key] = variable.Value;
                }
            }

            _instances[instance.InstanceId] = instance;

            // Start execution
            _ = Task.Run(async () => await ExecuteWorkflowAsync(instance, workflow));

            return instance.InstanceId;
        }

        private async Task ExecuteWorkflowAsync(WorkflowInstance instance, WorkflowDefinition workflow)
        {
            try
            {
                // Find start step
                var currentStep = workflow.Steps.FirstOrDefault(s => s.Type == StepType.Start) 
                    ?? workflow.Steps.FirstOrDefault();

                while (currentStep != null && instance.Status == WorkflowStatus.Running)
                {
                    instance.CurrentStepId = currentStep.StepId;
                    
                    // Log step execution
                    LogStep(instance, currentStep, $"Executing step: {currentStep.StepName}");

                    // Execute step with timeout and retry
                    var result = await ExecuteStepWithRetryAsync(currentStep, instance);

                    if (!result.Success)
                    {
                        // Handle error
                        if (!string.IsNullOrEmpty(currentStep.ErrorHandler))
                        {
                            currentStep = workflow.Steps.FirstOrDefault(s => s.StepId == currentStep.ErrorHandler);
                            continue;
                        }
                        else
                        {
                            instance.Status = WorkflowStatus.Failed;
                            instance.ErrorMessage = result.ErrorMessage;
                            break;
                        }
                    }

                    // Determine next step
                    currentStep = await DetermineNextStepAsync(currentStep, instance, workflow, result);

                    if (currentStep?.Type == StepType.End)
                    {
                        instance.Status = WorkflowStatus.Completed;
                        break;
                    }
                }

                instance.EndTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error executing workflow {workflow.WorkflowName}");
                instance.Status = WorkflowStatus.Failed;
                instance.ErrorMessage = ex.Message;
                instance.EndTime = DateTime.Now;
            }
        }

        private async Task<StepResult> ExecuteStepWithRetryAsync(WorkflowStep step, WorkflowInstance instance)
        {
            var maxRetries = step.MaxRetries ?? 1;
            var retryCount = 0;
            StepResult? result = null;

            while (retryCount < maxRetries)
            {
                try
                {
                    var cts = new CancellationTokenSource();
                    if (step.TimeoutSeconds.HasValue)
                    {
                        cts.CancelAfter(TimeSpan.FromSeconds(step.TimeoutSeconds.Value));
                    }

                    result = await ExecuteStepAsync(step, instance, cts.Token);
                    
                    if (result.Success)
                        break;
                }
                catch (OperationCanceledException)
                {
                    result = new StepResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Step execution timeout" 
                    };
                }
                catch (Exception ex)
                {
                    result = new StepResult 
                    { 
                        Success = false, 
                        ErrorMessage = ex.Message 
                    };
                }

                retryCount++;
                
                if (retryCount < maxRetries && result != null && !result.Success)
                {
                    LogStep(instance, step, $"Retry {retryCount} of {maxRetries}", true);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount))); // Exponential backoff
                }
            }

            return result ?? new StepResult { Success = false, ErrorMessage = "Unknown error" };
        }

        private async Task<StepResult> ExecuteStepAsync(WorkflowStep step, WorkflowInstance instance, CancellationToken cancellationToken)
        {
            switch (step.Type)
            {
                case StepType.Action:
                    return await ExecuteActionStepAsync(step, instance);
                    
                case StepType.Decision:
                    return await ExecuteDecisionStepAsync(step, instance);
                    
                case StepType.Loop:
                    return await ExecuteLoopStepAsync(step, instance);
                    
                case StepType.Parallel:
                    return await ExecuteParallelStepAsync(step, instance);
                    
                case StepType.Wait:
                    return await ExecuteWaitStepAsync(step, instance);
                    
                case StepType.SubWorkflow:
                    return await ExecuteSubWorkflowStepAsync(step, instance);
                    
                default:
                    return new StepResult { Success = true };
            }
        }

        private async Task<StepResult> ExecuteActionStepAsync(WorkflowStep step, WorkflowInstance instance)
        {
            var actionType = step.Parameters.GetValueOrDefault("action")?.ToString()?.ToLower() ?? "";
            
            if (_stepHandlers.TryGetValue(actionType, out var handler))
            {
                return await handler(step, instance);
            }

            return new StepResult 
            { 
                Success = false, 
                ErrorMessage = $"No handler found for action type: {actionType}" 
            };
        }

        private async Task<StepResult> ExecuteDecisionStepAsync(WorkflowStep step, WorkflowInstance instance)
        {
            if (string.IsNullOrEmpty(step.Condition))
            {
                return new StepResult { Success = true, Data = new Dictionary<string, object> { ["decision"] = true } };
            }

            // Evaluate condition using rule engine
            var result = await _ruleEngine.ExecuteRulesAsync("workflow", instance.Context, RuleType.Validation);
            
            return new StepResult 
            { 
                Success = true, 
                Data = new Dictionary<string, object> { ["decision"] = result.Success } 
            };
        }

        private async Task<StepResult> ExecuteLoopStepAsync(WorkflowStep step, WorkflowInstance instance)
        {
            // Implementation for loop execution
            await Task.CompletedTask;
            return new StepResult { Success = true };
        }

        private async Task<StepResult> ExecuteParallelStepAsync(WorkflowStep step, WorkflowInstance instance)
        {
            // Implementation for parallel execution
            var tasks = new List<Task<StepResult>>();
            // Execute parallel steps...
            await Task.WhenAll(tasks);
            return new StepResult { Success = true };
        }

        private async Task<StepResult> ExecuteWaitStepAsync(WorkflowStep step, WorkflowInstance instance)
        {
            var duration = step.Parameters.GetValueOrDefault("duration", 1000);
            await Task.Delay(Convert.ToInt32(duration));
            return new StepResult { Success = true };
        }

        private async Task<StepResult> ExecuteSubWorkflowStepAsync(WorkflowStep step, WorkflowInstance instance)
        {
            var subWorkflowId = step.Parameters.GetValueOrDefault("workflowId")?.ToString();
            if (string.IsNullOrEmpty(subWorkflowId))
            {
                return new StepResult { Success = false, ErrorMessage = "Sub-workflow ID not specified" };
            }

            var subInstanceId = await StartWorkflowAsync(subWorkflowId, instance.Context);
            
            // Wait for sub-workflow to complete
            // Implementation needed...
            
            return new StepResult { Success = true };
        }

        private async Task<WorkflowStep?> DetermineNextStepAsync(
            WorkflowStep currentStep, 
            WorkflowInstance instance, 
            WorkflowDefinition workflow,
            StepResult result)
        {
            if (!currentStep.NextSteps.Any())
                return null;

            string nextStepId;

            if (currentStep.Type == StepType.Decision && result.Data != null)
            {
                var decision = result.Data.GetValueOrDefault("decision", false);
                nextStepId = Convert.ToBoolean(decision) 
                    ? currentStep.NextSteps.FirstOrDefault() ?? ""
                    : currentStep.NextSteps.Skip(1).FirstOrDefault() ?? "";
            }
            else
            {
                nextStepId = currentStep.NextSteps.FirstOrDefault() ?? "";
            }

            return workflow.Steps.FirstOrDefault(s => s.StepId == nextStepId);
        }

        private void LogStep(WorkflowInstance instance, WorkflowStep step, string message, bool isError = false)
        {
            var log = new WorkflowLog
            {
                StepId = step.StepId,
                StepName = step.StepName,
                Message = message,
                IsError = isError
            };

            instance.Logs.Add(log);
            
            if (isError)
                Log.Error($"Workflow {instance.InstanceId}: {message}");
            else
                Log.Information($"Workflow {instance.InstanceId}: {message}");
        }

        // Default step handlers
        private async Task<StepResult> LogHandler(WorkflowStep step, WorkflowInstance instance)
        {
            var message = step.Parameters.GetValueOrDefault("message")?.ToString() ?? "";
            Log.Information($"Workflow log: {message}");
            return await Task.FromResult(new StepResult { Success = true });
        }

        private async Task<StepResult> SaveHandler(WorkflowStep step, WorkflowInstance instance)
        {
            // Save data to database
            return await Task.FromResult(new StepResult { Success = true });
        }

        private async Task<StepResult> EmailHandler(WorkflowStep step, WorkflowInstance instance)
        {
            // Send email
            return await Task.FromResult(new StepResult { Success = true });
        }

        private async Task<StepResult> CalculateHandler(WorkflowStep step, WorkflowInstance instance)
        {
            // Perform calculation
            return await Task.FromResult(new StepResult { Success = true });
        }

        private async Task<StepResult> ValidateHandler(WorkflowStep step, WorkflowInstance instance)
        {
            // Validate data
            return await Task.FromResult(new StepResult { Success = true });
        }

        private async Task<StepResult> TransformHandler(WorkflowStep step, WorkflowInstance instance)
        {
            // Transform data
            return await Task.FromResult(new StepResult { Success = true });
        }

        private async Task<StepResult> QueryHandler(WorkflowStep step, WorkflowInstance instance)
        {
            // Execute query
            return await Task.FromResult(new StepResult { Success = true });
        }

        private async Task<StepResult> UpdateHandler(WorkflowStep step, WorkflowInstance instance)
        {
            // Update data
            return await Task.FromResult(new StepResult { Success = true });
        }

        private async Task<StepResult> DeleteHandler(WorkflowStep step, WorkflowInstance instance)
        {
            // Delete data
            return await Task.FromResult(new StepResult { Success = true });
        }

        private async Task<StepResult> NotifyHandler(WorkflowStep step, WorkflowInstance instance)
        {
            // Send notification
            return await Task.FromResult(new StepResult { Success = true });
        }

        public WorkflowInstance? GetInstance(string instanceId)
        {
            return _instances.TryGetValue(instanceId, out var instance) ? instance : null;
        }

        public List<WorkflowInstance> GetActiveInstances()
        {
            return _instances.Values
                .Where(i => i.Status == WorkflowStatus.Running || i.Status == WorkflowStatus.Paused)
                .ToList();
        }
    }

    public class StepResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object>? Data { get; set; }
    }
}

// پایان فایل: Core/WorkflowEngine/WorkflowEngine.cs