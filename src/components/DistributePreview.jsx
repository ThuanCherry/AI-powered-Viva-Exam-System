import { useMemo, useState } from "react";

const PREVIEW_STUDENTS = 3;

// Bộ sinh số ngẫu nhiên theo seed, để kết quả xem trước ổn định giữa các lần render.
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

function drawQuestions(pool, count, seed) {
  const rng = mulberry32(seed);
  const copy = [...pool];
  for (let i = copy.length - 1; i > 0; i--) {
    const j = Math.floor(rng() * (i + 1));
    [copy[i], copy[j]] = [copy[j], copy[i]];
  }
  return copy.slice(0, count);
}

function difficultyMix(pool) {
  const mix = { Easy: 0, Medium: 0, Hard: 0 };
  pool.forEach((q) => {
    if (q.difficulty in mix) mix[q.difficulty] += 1;
  });
  return mix;
}

export default function DistributionPreview({ pool, mainCount, maxFollowUps, strategy }) {
  const [seed, setSeed] = useState(1);

  const canPreview = Number.isInteger(mainCount) && mainCount >= 1 && pool.length >= mainCount;

  const students = useMemo(() => {
    if (!canPreview || strategy !== "RANDOM") return [];
    return Array.from({ length: PREVIEW_STUDENTS }, (_, i) =>
      drawQuestions(pool, mainCount, seed * 1000 + i)
    );
  }, [pool, mainCount, strategy, seed, canPreview]);

  const overlap = useMemo(() => {
    if (students.length === 0) return null;
    const counts = new Map();
    students.forEach((set) => set.forEach((q) => counts.set(q.id, (counts.get(q.id) ?? 0) + 1)));
    const shared = [...counts.values()].filter((n) => n === students.length).length;
    return { unique: counts.size, shared };
  }, [students]);

  const mix = useMemo(() => difficultyMix(pool), [pool]);

  return (
    <section className="qc-card" aria-labelledby="qc-preview-title">
      <div className="qc-preview__header">
        <h2 className="qc-card__title" id="qc-preview-title">
          Distribution preview
        </h2>
        {strategy === "RANDOM" && canPreview && (
          <button type="button" className="qc-btn qc-btn--ghost qc-preview__shuffle" onClick={() => setSeed((s) => s + 1)}>
            Shuffle
          </button>
        )}
      </div>

      <p className="qc-preview__note">
        A simulation in this page, not the server&apos;s actual draw. Real questions may differ.
      </p>

      {!canPreview && (
        <p className="qc-preview__empty">
          Choose a valid number of main questions and add at least that many questions to the pool
          to see a preview.
        </p>
      )}

      {canPreview && strategy === "ADAPTIVE" && (
        <div>
          <p className="qc-preview__text">
            With the Adaptive strategy, each next question depends on how the student answers, so a
            fixed list cannot be shown. This is the difficulty mix the system can choose from:
          </p>
          <ul className="qc-preview__mix">
            {Object.entries(mix).map(([level, count]) => (
              <li key={level}>
                <span className={`qc-badge is-${level.toLowerCase()}`}>{level}</span>
                <strong>{count}</strong> in pool
              </li>
            ))}
          </ul>
        </div>
      )}

      {canPreview && strategy === "RANDOM" && (
        <>
          <div className="qc-preview__grid">
            {students.map((set, index) => (
              <div className="qc-preview__student" key={index}>
                <h3>Student {index + 1}</h3>
                <ol>
                  {set.map((q) => (
                    <li key={q.id}>
                      <span>{q.text}</span>
                      <span className={`qc-badge is-${q.difficulty.toLowerCase()}`}>
                        {q.difficulty}
                      </span>
                    </li>
                  ))}
                </ol>
                {maxFollowUps > 0 && (
                  <p className="qc-preview__follow">
                    + up to {maxFollowUps} follow-up{maxFollowUps === 1 ? "" : "s"} per question
                  </p>
                )}
              </div>
            ))}
          </div>

          {overlap && (
            <p className="qc-preview__text" role="status">
              {overlap.unique} different questions across {students.length} students;{" "}
              <strong>{overlap.shared}</strong> {overlap.shared === 1 ? "is" : "are"} the same for
              everyone.
            </p>
          )}
        </>
      )}
    </section>
  );
}