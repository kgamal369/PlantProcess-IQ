// ============================================================================
// Structured difference between two immutable assessment versions.
//
// The diff is computed from the two stored reports. There is no mutable diff
// table: a difference is a function of two immutable facts, so persisting it
// would create a third thing that can disagree with them.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PlantProcess.Application.CustomerAssessment
{
    public static class AssessmentChangeKinds
    {
        public const string SectionAdded = "SECTION_ADDED";
        public const string SectionRemoved = "SECTION_REMOVED";
        public const string SectionStatusChanged = "SECTION_STATUS_CHANGED";
        public const string EvidenceAdded = "EVIDENCE_ADDED";
        public const string EvidenceRemoved = "EVIDENCE_REMOVED";
        public const string CandidateAdded = "CANDIDATE_ADDED";
        public const string CandidateRemoved = "CANDIDATE_REMOVED";
        public const string BlockerIntroduced = "BLOCKER_INTRODUCED";
        public const string BlockerResolved = "BLOCKER_RESOLVED";
    }

    public sealed class AssessmentDiffEntry
    {
        public string SectionCode { get; set; } = string.Empty;
        public string ChangeKind { get; set; } = string.Empty;
        public string Before { get; set; } = string.Empty;
        public string After { get; set; } = string.Empty;
    }

    public sealed class CustomerAssessmentDiff
    {
        public string LineageCode { get; set; } = string.Empty;
        public int FromVersionNumber { get; set; }
        public int ToVersionNumber { get; set; }
        public bool ReadinessChanged { get; set; }

        public IReadOnlyList<AssessmentDiffEntry> Entries { get; set; }
            = Array.Empty<AssessmentDiffEntry>();
    }

    public static class CustomerAssessmentDiffCalculator
    {
        public static CustomerAssessmentDiff Compute(
            CustomerAssessmentReport from,
            int fromVersionNumber,
            CustomerAssessmentReport to,
            int toVersionNumber)
        {
            if (from == null)
            {
                throw new ArgumentNullException(nameof(from));
            }

            if (to == null)
            {
                throw new ArgumentNullException(nameof(to));
            }

            Dictionary<string, AssessmentSection> fromSections = Index(from);
            Dictionary<string, AssessmentSection> toSections = Index(to);

            var entries = new List<AssessmentDiffEntry>();

            List<string> allCodes = fromSections.Keys
                .Concat(toSections.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToList();

            foreach (string code in allCodes)
            {
                AssessmentSection before;
                AssessmentSection after;
                bool hasBefore = fromSections.TryGetValue(code, out before);
                bool hasAfter = toSections.TryGetValue(code, out after);

                if (!hasBefore)
                {
                    entries.Add(Entry(code, AssessmentChangeKinds.SectionAdded, string.Empty, after.StatusCode));
                    continue;
                }

                if (!hasAfter)
                {
                    entries.Add(Entry(code, AssessmentChangeKinds.SectionRemoved, before.StatusCode, string.Empty));
                    continue;
                }

                if (before.Status != after.Status)
                {
                    entries.Add(Entry(code, AssessmentChangeKinds.SectionStatusChanged, before.StatusCode, after.StatusCode));
                }

                CompareSet(
                    entries, code,
                    before.Evidence.Select(EvidenceKey),
                    after.Evidence.Select(EvidenceKey),
                    AssessmentChangeKinds.EvidenceAdded,
                    AssessmentChangeKinds.EvidenceRemoved);

                CompareSet(
                    entries, code,
                    before.Candidates.Select(CandidateKey),
                    after.Candidates.Select(CandidateKey),
                    AssessmentChangeKinds.CandidateAdded,
                    AssessmentChangeKinds.CandidateRemoved);

                CompareSet(
                    entries, code,
                    before.Blockers.Select(BlockerKey),
                    after.Blockers.Select(BlockerKey),
                    AssessmentChangeKinds.BlockerIntroduced,
                    AssessmentChangeKinds.BlockerResolved);
            }

            return new CustomerAssessmentDiff
            {
                LineageCode = to.LineageCode,
                FromVersionNumber = fromVersionNumber,
                ToVersionNumber = toVersionNumber,
                ReadinessChanged = ConcludedCount(from) != ConcludedCount(to),
                Entries = entries
            };
        }

        private static void CompareSet(
            List<AssessmentDiffEntry> entries,
            string sectionCode,
            IEnumerable<string> before,
            IEnumerable<string> after,
            string addedKind,
            string removedKind)
        {
            var beforeSet = new HashSet<string>(before, StringComparer.Ordinal);
            var afterSet = new HashSet<string>(after, StringComparer.Ordinal);

            List<string> added = afterSet.Except(beforeSet, StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToList();

            List<string> removed = beforeSet.Except(afterSet, StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToList();

            foreach (string key in added)
            {
                entries.Add(Entry(sectionCode, addedKind, string.Empty, key));
            }

            foreach (string key in removed)
            {
                entries.Add(Entry(sectionCode, removedKind, key, string.Empty));
            }
        }

        private static int ConcludedCount(CustomerAssessmentReport report)
        {
            return report.Sections.Count(s =>
                s.Status == AssessmentStatus.Known || s.Status == AssessmentStatus.NotApplicable);
        }

        private static Dictionary<string, AssessmentSection> Index(CustomerAssessmentReport report)
        {
            var result = new Dictionary<string, AssessmentSection>(StringComparer.Ordinal);
            foreach (AssessmentSection section in report.Sections)
            {
                result[section.SectionCode] = section;
            }

            return result;
        }

        private static string EvidenceKey(AssessmentEvidence evidence)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}|{2}",
                evidence.EvidenceCode,
                evidence.IntakeRef ?? string.Empty,
                evidence.Statement);
        }

        private static string CandidateKey(AssessmentCandidate candidate)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}",
                candidate.CandidateKind,
                candidate.CandidateCode);
        }

        private static string BlockerKey(AssessmentBlocker blocker)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}",
                blocker.BlockerCode,
                blocker.RequiredInput);
        }

        private static AssessmentDiffEntry Entry(string sectionCode, string kind, string before, string after)
        {
            return new AssessmentDiffEntry
            {
                SectionCode = sectionCode,
                ChangeKind = kind,
                Before = before,
                After = after
            };
        }
    }
}
