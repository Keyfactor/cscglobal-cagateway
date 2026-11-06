using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Keyfactor.AnyGateway.CscGlobal
{
    public enum FlowStepStatus
    {
        Success,
        Failed,
        Skipped,
        InProgress
    }

    public class FlowStep
    {
        public string Name { get; set; }
        public FlowStepStatus Status { get; set; }
        public string Detail { get; set; }
        public long ElapsedMs { get; set; }
        public List<FlowStep> Children { get; } = new List<FlowStep>();
    }

    /// <summary>
    ///     Tracks the high-level flow of an operation and renders a step diagram to the Trace log when disposed.
    ///     Steps should only carry non-sensitive detail (IDs, counts, decisions), never CSRs, keys or contact data.
    ///     Usage:
    ///       using (var flow = new FlowLogger(m => Logger.Trace(m), "Enroll-New"))
    ///       {
    ///           flow.Step("ValidateInput");
    ///           var result = flow.Step("SubmitRegistration", () => client.Submit(...));
    ///           flow.Fail("CreateOrder", "API returned 400");
    ///       }
    /// </summary>
    public sealed class FlowLogger : IDisposable
    {
        private readonly Action<string> _log;
        private readonly string _flowName;
        private readonly Stopwatch _totalTimer;
        private readonly List<FlowStep> _steps = new List<FlowStep>();
        private FlowStep _currentParent;
        private bool _disposed;

        public FlowLogger(Action<string> log, string flowName)
        {
            _log = log ?? (_ => { });
            _flowName = flowName;
            _totalTimer = Stopwatch.StartNew();
            Write($"===== FLOW START: {_flowName} =====");
        }

        /// <summary>True if any recorded step failed.</summary>
        public bool HasFailures => _steps.Any(s => s.Status == FlowStepStatus.Failed ||
                                                   s.Children.Any(c => c.Status == FlowStepStatus.Failed));

        /// <summary>Record a completed step.</summary>
        public FlowLogger Step(string name, string detail = null)
        {
            AddStep(new FlowStep { Name = name, Status = FlowStepStatus.Success, Detail = detail });
            Write($"  [{_flowName}] {name} ... OK{(detail != null ? $" ({detail})" : "")}");
            return this;
        }

        /// <summary>Run an action as a timed step. Failures are recorded and rethrown.</summary>
        public FlowLogger Step(string name, Action action, string detail = null)
        {
            Step<object>(name, () =>
            {
                action();
                return null;
            }, detail);
            return this;
        }

        /// <summary>Run a function as a timed step and return its result. Failures are recorded and rethrown.</summary>
        public T Step<T>(string name, Func<T> func, string detail = null)
        {
            var sw = Stopwatch.StartNew();
            var step = new FlowStep { Name = name, Detail = detail };
            try
            {
                Write($"  [{_flowName}] {name} ...");
                var result = func();
                sw.Stop();
                step.Status = FlowStepStatus.Success;
                step.ElapsedMs = sw.ElapsedMilliseconds;
                AddStep(step);
                Write($"  [{_flowName}] {name} ... OK ({sw.ElapsedMilliseconds}ms){(detail != null ? $" {detail}" : "")}");
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                step.Status = FlowStepStatus.Failed;
                step.ElapsedMs = sw.ElapsedMilliseconds;
                step.Detail = ex.Message;
                AddStep(step);
                Write($"  [{_flowName}] {name} ... FAILED ({sw.ElapsedMilliseconds}ms): {ex.Message}");
                throw;
            }
        }

        /// <summary>Record a failed step without throwing.</summary>
        public FlowLogger Fail(string name, string reason = null)
        {
            AddStep(new FlowStep { Name = name, Status = FlowStepStatus.Failed, Detail = reason });
            Write($"  [{_flowName}] {name} ... FAILED{(reason != null ? $": {reason}" : "")}");
            return this;
        }

        /// <summary>Record a skipped step.</summary>
        public FlowLogger Skip(string name, string reason = null)
        {
            AddStep(new FlowStep { Name = name, Status = FlowStepStatus.Skipped, Detail = reason });
            Write($"  [{_flowName}] {name} ... SKIPPED{(reason != null ? $": {reason}" : "")}");
            return this;
        }

        /// <summary>Start a branch (group of child steps).</summary>
        public FlowLogger Branch(string name)
        {
            var step = new FlowStep { Name = name, Status = FlowStepStatus.InProgress };
            AddStep(step);
            _currentParent = step;
            Write($"  [{_flowName}] >> Branch: {name}");
            return this;
        }

        /// <summary>End the current branch.</summary>
        public FlowLogger EndBranch()
        {
            if (_currentParent != null && _currentParent.Status == FlowStepStatus.InProgress)
                _currentParent.Status = _currentParent.Children.Any(c => c.Status == FlowStepStatus.Failed)
                    ? FlowStepStatus.Failed
                    : FlowStepStatus.Success;
            _currentParent = null;
            return this;
        }

        private void Write(string message)
        {
            try
            {
                _log(message);
            }
            catch
            {
                // Flow logging must never break the operation being logged.
            }
        }

        private void AddStep(FlowStep step)
        {
            if (_currentParent != null)
                _currentParent.Children.Add(step);
            else
                _steps.Add(step);
        }

        private string RenderFlow()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"  ===== FLOW: {_flowName} ({_totalTimer.ElapsedMilliseconds}ms total) =====");
            sb.AppendLine();

            for (var i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                sb.AppendLine($"    {GetStatusIcon(step.Status)} {step.Name}{FormatTiming(step)}{FormatDetail(step)}");

                foreach (var child in step.Children)
                {
                    sb.AppendLine("    |");
                    sb.AppendLine($"    +-- {GetStatusIcon(child.Status)} {child.Name}{FormatTiming(child)}{FormatDetail(child)}");
                }

                if (i < _steps.Count - 1)
                {
                    sb.AppendLine("    |");
                    sb.AppendLine("    v");
                }
            }

            sb.AppendLine();
            var finalStatus = _steps.Count > 0 && _steps.Last().Status == FlowStepStatus.Failed
                ? "FAILED"
                : HasFailures ? "PARTIAL FAILURE" : "SUCCESS";
            sb.AppendLine($"  ===== FLOW RESULT: {finalStatus} =====");
            return sb.ToString();
        }

        private static string FormatTiming(FlowStep step)
        {
            return step.ElapsedMs > 0 ? $" ({step.ElapsedMs}ms)" : "";
        }

        private static string FormatDetail(FlowStep step)
        {
            return !string.IsNullOrEmpty(step.Detail) ? $" [{step.Detail}]" : "";
        }

        private static string GetStatusIcon(FlowStepStatus status)
        {
            switch (status)
            {
                case FlowStepStatus.Success: return "[OK]";
                case FlowStepStatus.Failed: return "[FAIL]";
                case FlowStepStatus.Skipped: return "[SKIP]";
                case FlowStepStatus.InProgress: return "[...]";
                default: return "[?]";
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _totalTimer.Stop();
            Write(RenderFlow());
        }
    }
}
