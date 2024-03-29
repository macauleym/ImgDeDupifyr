using System;
using System.Collections.Generic;
using ImgDiff.Builders;
using ImgDiff.Models;
using ImgDiff.Monads;

namespace ImgDiff.Factories
{
    public class ExecutionStatusFactory
    {
        public ExecutionStatus ConstructSuccess(
            ComparisonRequest original,
            Option<List<DeDupifyrResult>> results,
            Action printInstructions)
        {
            var statusBuilder = InitialBuilder(original);

            return statusBuilder
                .HasResults(results)
                .WithPrintInstructions(printInstructions)
                .Build();
        }

        public ExecutionStatus ConstructUpdated(ComparisonRequest original, ComparisonOptions updatedOptions)
        {
            var statusBuilder = InitialBuilder(original);

            return statusBuilder
                .OverridesOptionsWith(updatedOptions)
                .Build();
        }

        public ExecutionStatus ConstructTerminated()
        {
            var statusBuilder = new ExecutionStatusBuilder();

            return statusBuilder
                .ShouldTerminate(true)
                .Build();
        }

        public ExecutionStatus ConstructNoOp()
        {
            var statusBuilder = new ExecutionStatusBuilder();

            return statusBuilder.Build();
        }

        public ExecutionStatus ConstructFaulted(Exception faultReason)
        {
            var statusBuilder = new ExecutionStatusBuilder();

            return statusBuilder
                .HasFaulted(true)
                .FaultedWithException(faultReason)
                .Build();
        }

        ExecutionStatusBuilder InitialBuilder(ComparisonRequest original)
        {
            return new ExecutionStatusBuilder()
                .WithOriginalRequest(original);
        }
    }
}