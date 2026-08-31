// ============================================================================
// The customer assessment service contract.
//
// Application names no Infrastructure type. The persistence records that back
// this contract live in Infrastructure and are never returned across it.
//
// TENANCY
//   TenantId is supplied by the caller from authenticated request identity.
//   This contract has no fallback, demo or generated tenant and no overload
//   that omits one.
//
// AUTHORITY BARRIER
//   Executing an assessment may write assessment lineage and assessment
//   version rows and nothing else. This interface deliberately exposes no verb
//   that could publish, register or promote a candidate.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlantProcess.Application.CustomerAssessment
{
    public enum AssessmentRefusalReason
    {
        None = 0,
        TenantNotResolved = 1,
        TenantMismatch = 2,
        AssessmentNotFound = 3,
        VersionNotFound = 4,
        IntakeInvalid = 5
    }

    /// <summary>
    /// One persisted, immutable assessment version.
    /// </summary>
    public sealed class CustomerAssessmentVersionResult
    {
        public Guid AssessmentId { get; set; }
        public Guid AssessmentVersionId { get; set; }
        public string LineageCode { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public string ContractVersion { get; set; } = string.Empty;
        public string RuleVersion { get; set; } = string.Empty;
        public string SemanticFingerprint { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }

        /// <summary>
        /// True when this call reused an existing version because the
        /// assessment truth had not changed. The history records changes in
        /// assessment truth, not button presses.
        /// </summary>
        public bool Reused { get; set; }

        public CustomerAssessmentReport Report { get; set; } = new CustomerAssessmentReport();
    }

    /// <summary>
    /// Outcome envelope. A refusal carries a reason rather than a null result,
    /// so a caller can never mistake absence for success.
    /// </summary>
    public sealed class AssessmentOutcome<T> where T : class
    {
        public bool Succeeded { get; private set; }
        public AssessmentRefusalReason Reason { get; private set; }
        public T? Value { get; private set; }

        public static AssessmentOutcome<T> Success(T value)
        {
            return new AssessmentOutcome<T>
            {
                Succeeded = true,
                Reason = AssessmentRefusalReason.None,
                Value = value
            };
        }

        public static AssessmentOutcome<T> Refused(AssessmentRefusalReason reason)
        {
            if (reason == AssessmentRefusalReason.None)
            {
                throw new ArgumentException(
                    "A refusal must name a reason.",
                    nameof(reason));
            }

            return new AssessmentOutcome<T>
            {
                Succeeded = false,
                Reason = reason,
                Value = null
            };
        }
    }

    public interface ICustomerAssessmentService
    {
        /// <summary>
        /// Assess an intake. Identical normalised intake under identical
        /// semantic versions reuses the existing version. A changed intake or
        /// a changed rule version creates the next immutable version.
        /// </summary>
        Task<AssessmentOutcome<CustomerAssessmentVersionResult>> AssessAsync(
            Guid tenantId,
            CustomerIntake intake,
            CancellationToken cancellationToken);

        /// <summary>Latest immutable version for a lineage inside one tenant.</summary>
        Task<AssessmentOutcome<CustomerAssessmentVersionResult>> GetLatestAsync(
            Guid tenantId,
            string lineageCode,
            CancellationToken cancellationToken);

        /// <summary>One historical immutable version, exactly as first produced.</summary>
        Task<AssessmentOutcome<CustomerAssessmentVersionResult>> GetVersionAsync(
            Guid tenantId,
            string lineageCode,
            int versionNumber,
            CancellationToken cancellationToken);

        /// <summary>Structured differences between two immutable versions.</summary>
        Task<AssessmentOutcome<CustomerAssessmentDiff>> GetDiffAsync(
            Guid tenantId,
            string lineageCode,
            int fromVersionNumber,
            int toVersionNumber,
            CancellationToken cancellationToken);
    }
}
