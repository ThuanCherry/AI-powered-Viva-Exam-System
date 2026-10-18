import { useMemo, useState } from "react";
import "../styles/CandidateQuestionDistribution.css";

// Simple PRNG for deterministic preview shuffling
function mulberry32(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// Client-side anti-overlap question generator for simulation mode
function simulateAntiOverlapDraw(pool, candidatesList, mainCount, strategy, seed) {
  if (!pool || pool.length === 0 || candidatesList.length === 0 || mainCount <= 0) {
    return [];
  }

  const rng = mulberry32(seed);
  const k = Math.min(mainCount, pool.length);
  const results = [];
  let previousAssignedIds = new Set();

  candidatesList.forEach((cand, index) => {
    let selected = [];

    if (strategy === "ADAPTIVE") {
      // Prioritize non-overlapping Medium/Understand questions
      const nonOverlap = pool.filter((q) => !previousAssignedIds.has(String(q.id)));
      if (nonOverlap.length >= k) {
        const medium = nonOverlap.filter((q) => q.difficulty === "Medium").sort(() => rng() - 0.5);
        const others = nonOverlap.filter((q) => q.difficulty !== "Medium").sort(() => rng() - 0.5);
        selected = [...medium, ...others].slice(0, k);
      } else {
        const needed = k - nonOverlap.length;
        const overlapFill = pool
          .filter((q) => previousAssignedIds.has(String(q.id)))
          .sort(() => rng() - 0.5)
          .slice(0, needed);
        selected = [...nonOverlap, ...overlapFill];
      }
    } else {
      // RANDOM anti-overlap draw
      const nonOverlap = pool
        .filter((q) => !previousAssignedIds.has(String(q.id)))
        .sort(() => rng() - 0.5);

      if (nonOverlap.length >= k) {
        selected = nonOverlap.slice(0, k);
      } else {
        const needed = k - nonOverlap.length;
        const overlapFill = pool
          .filter((q) => previousAssignedIds.has(String(q.id)))
          .sort(() => rng() - 0.5)
          .slice(0, needed);
        selected = [...nonOverlap, ...overlapFill].sort(() => rng() - 0.5);
      }
    }

    previousAssignedIds = new Set(selected.map((q) => String(q.id)));

    results.push({
      orderNo: index + 1,
      studentName: cand.student || cand.studentName || `Candidate #${index + 1}`,
      studentEmail: cand.studentEmail || cand.email || "Candidate",
      scheduledTime: cand.start
        ? `Slot: ${cand.start} - ${cand.end}`
        : cand.scheduledTime
        ? `Slot: ${new Date(cand.scheduledTime).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`
        : "Pending slot",
      questions: selected.map((q, qIndex) => ({
        orderNo: qIndex + 1,
        questionId: q.id,
        questionText: q.text,
        difficulty: q.difficulty || "Medium",
        bloomLevel: q.bloomLevel || "Understand",
      })),
    });
  });

  return results;
}

export default function CandidateQuestionDistribution({
  candidates = [],
  schedule = [],
  pool = [],
  mainCount = 3,
  maxFollowUps = 2,
  strategy = "RANDOM",
  isLocked = false,
  onRegenerate,
  isRegenerating = false,
}) {
  const [seed, setSeed] = useState(42);

  // Determine whether to show actual DB questions or live simulation
  const hasDbQuestions =
    isLocked &&
    candidates.length > 0 &&
    candidates.some((c) => c.questions && c.questions.length > 0);

  // Compute displayed list
  const displayItems = useMemo(() => {
    if (hasDbQuestions) {
      return candidates.map((cand, index) => ({
        orderNo: index + 1,
        studentName: cand.studentName || `Candidate #${cand.studentId}`,
        studentEmail: cand.studentEmail || "Student",
        scheduledTime: cand.scheduledTime
          ? `Slot: ${new Date(cand.scheduledTime).toLocaleTimeString([], {
              hour: "2-digit",
              minute: "2-digit",
            })}`
          : "Scheduled",
        questions: (cand.questions || []).map((q) => ({
          orderNo: q.orderNo,
          questionId: q.questionId,
          questionText: q.questionText,
          difficulty: q.difficulty || "Medium",
          bloomLevel: q.bloomLevel || "Understand",
        })),
      }));
    }

    // Live preview from current schedule & pool
    const candidatesToSimulate =
      schedule.length > 0
        ? schedule
        : candidates.length > 0
        ? candidates
        : [
            { student: "Candidate 1 (Simulated)", email: "student1@fpt.edu.vn", start: "08:00", end: "08:20" },
            { student: "Candidate 2 (Simulated)", email: "student2@fpt.edu.vn", start: "08:25", end: "08:45" },
            { student: "Candidate 3 (Simulated)", email: "student3@fpt.edu.vn", start: "08:50", end: "09:10" },
          ];

    return simulateAntiOverlapDraw(pool, candidatesToSimulate, mainCount, strategy, seed);
  }, [hasDbQuestions, candidates, schedule, pool, mainCount, strategy, seed]);

  // Compute Consecutive Overlap & Distinct Metrics
  const { consecutiveOverlaps, distinctQuestionsCount } = useMemo(() => {
    if (displayItems.length === 0) return { consecutiveOverlaps: 0, distinctQuestionsCount: 0 };

    let overlaps = 0;
    const allAssignedIds = new Set();

    for (let i = 0; i < displayItems.length; i++) {
      const currentIds = new Set(displayItems[i].questions.map((q) => String(q.questionId)));
      currentIds.forEach((id) => allAssignedIds.add(id));

      if (i > 0) {
        const prevIds = new Set(displayItems[i - 1].questions.map((q) => String(q.questionId)));
        let sharedInPair = 0;
        currentIds.forEach((id) => {
          if (prevIds.has(id)) sharedInPair++;
        });
        overlaps += sharedInPair;
      }
    }

    return {
      consecutiveOverlaps: overlaps,
      distinctQuestionsCount: allAssignedIds.size,
    };
  }, [displayItems]);

  const canPreview = pool.length >= mainCount && mainCount > 0;

  return (
    <section className="qc-card cqd-section" aria-labelledby="cqd-title">
      {/* Header */}
      <div className="cqd-header">
        <div className="cqd-title-wrap">
          <div className="cqd-title-row">
            <h2 className="cqd-title" id="cqd-title">
              Candidate Question Distribution & Anti-Overlap Verification
            </h2>
            {hasDbQuestions ? (
              <span className="badge-status is-ready" title="Assigned and stored in database">
                ✓ Locked in Database
              </span>
            ) : (
              <span className="badge-status is-scheduled" title="Simulated based on current pool & slots">
                Live Distribution Preview
              </span>
            )}
          </div>
          <p className="cqd-subtitle">
            {hasDbQuestions
              ? "Official question sets locked in database. Consecutive candidate pairs have verified non-overlapping questions to prevent eavesdropping."
              : "Live preview of candidate question sets based on selected pool and order. Consecutive candidates get distinct questions."}
          </p>
        </div>

        <div className="cqd-actions">
          {hasDbQuestions ? (
            <button
              type="button"
              className="cqd-btn-shuffle"
              onClick={onRegenerate}
              disabled={isRegenerating}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67" />
              </svg>
              {isRegenerating ? "Regenerating..." : "Re-generate & Lock Sets"}
            </button>
          ) : (
            <button
              type="button"
              className="cqd-btn-shuffle"
              onClick={() => setSeed((s) => s + 1)}
              disabled={!canPreview}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M16 3h5v5M4 20L21 3M21 16v5h-5M15 15l6 6M4 4l5 5" />
              </svg>
              Shuffle Preview
            </button>
          )}
        </div>
      </div>

      {/* Verification Metrics Bar */}
      {canPreview && displayItems.length > 0 && (
        <div
          className={`cqd-metrics-bar ${consecutiveOverlaps === 0 ? "is-verified" : "is-warning"}`}
          role="status"
        >
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            {consecutiveOverlaps === 0 ? (
              <span>
                <strong>✓ Anti-Eavesdropping Verified:</strong> 0 overlapping questions between consecutive slots (100% Secure)
              </span>
            ) : (
              <span>
                <strong>⚠️ Notice:</strong> {consecutiveOverlaps} shared question(s) between consecutive candidates due to small pool size.
              </span>
            )}
          </div>

          <div className="cqd-metrics-stats">
            <span>
              <strong>{displayItems.length}</strong> candidates
            </span>
            <span>&bull;</span>
            <span>
              <strong>{distinctQuestionsCount}</strong> distinct questions from pool of {pool.length}
            </span>
            <span>&bull;</span>
            <span>Strategy: <strong>{strategy}</strong></span>
          </div>
        </div>
      )}

      {/* Empty State */}
      {!canPreview && (
        <div className="cqd-empty-box">
          <p>
            Please choose at least <strong>{mainCount}</strong> questions in the Question Pool above to view candidate distribution.
          </p>
        </div>
      )}

      {/* Card Grid */}
      {canPreview && displayItems.length > 0 && (
        <div className="cqd-grid">
          {displayItems.map((item) => (
            <div className="cqd-card" key={item.orderNo}>
              <div className="cqd-card-header">
                <div className="cqd-cand-info">
                  <span className="cqd-cand-order-name">
                    #{item.orderNo} {item.studentName}
                  </span>
                  <span className="cqd-cand-email">{item.studentEmail}</span>
                </div>
                <span className="cqd-slot-pill">{item.scheduledTime}</span>
              </div>

              <div className="cqd-card-body">
                {item.questions.map((q) => (
                  <div className="cqd-question-item" key={q.orderNo}>
                    <span className="cqd-question-text">
                      <strong>Q{q.orderNo}:</strong> {q.questionText}
                    </span>
                    <div className="cqd-question-tags">
                      <span className={`cqd-tag-diff is-${(q.difficulty || "medium").toLowerCase()}`}>
                        {q.difficulty}
                      </span>
                      {q.bloomLevel && (
                        <span className="cqd-tag-bloom">{q.bloomLevel}</span>
                      )}
                    </div>
                  </div>
                ))}
              </div>

              <div className="cqd-card-footer">
                <span>
                  {maxFollowUps > 0
                    ? `+ up to ${maxFollowUps} follow-ups per question`
                    : "No follow-up questions"}
                </span>
                <span>{item.questions.length} main Qs</span>
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
