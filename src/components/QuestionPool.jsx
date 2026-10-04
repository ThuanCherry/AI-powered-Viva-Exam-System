import { useEffect, useMemo, useRef, useState } from "react";
import { ALL_DIFFICULTIES, ALL_SUBJECTS, DIFFICULTIES, SUBJECTS } from "../data/questions.js";

function Select({ label, value, onChange, options }) {
  return (
    <label className="qc-select">
      <span className="qc-visually-hidden">{label}</span>
      <select value={value} onChange={(e) => onChange(e.target.value)}>
        {options.map((opt) => (
          <option key={opt} value={opt}>
            {opt}
          </option>
        ))}
      </select>
    </label>
  );
}

export default function QuestionPool({ questions, selectedIds, onChange, required, error }) {
  const [search, setSearch] = useState("");
  const [subject, setSubject] = useState(ALL_SUBJECTS);
  const [difficulty, setDifficulty] = useState(ALL_DIFFICULTIES);
  const selectAllRef = useRef(null);

  const selected = useMemo(() => new Set(selectedIds), [selectedIds]);

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase();
    return questions.filter(
      (q) =>
        q.text.toLowerCase().includes(term) &&
        (subject === ALL_SUBJECTS || q.subject === subject) &&
        (difficulty === ALL_DIFFICULTIES || q.difficulty === difficulty)
    );
  }, [questions, search, subject, difficulty]);

  const selectedVisible = visible.filter((q) => selected.has(q.id)).length;
  const allVisibleSelected = visible.length > 0 && selectedVisible === visible.length;

  useEffect(() => {
    if (selectAllRef.current) {
      selectAllRef.current.indeterminate = selectedVisible > 0 && !allVisibleSelected;
    }
  }, [selectedVisible, allVisibleSelected]);

  const toggleOne = (id) => {
    const next = new Set(selected);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    onChange([...next]);
  };

  const toggleVisible = () => {
    const next = new Set(selected);
    visible.forEach((q) => (allVisibleSelected ? next.delete(q.id) : next.add(q.id)));
    onChange([...next]);
  };

  const count = selected.size;
  const meetsRequirement = Number.isFinite(required) && count >= required;

  return (
    <section className="qc-card" aria-labelledby="qc-pool-title">
      <div className="qc-pool__header">
        <h2 className="qc-card__title" id="qc-pool-title">
          Question pool
        </h2>
        <p className={"qc-pool__count" + (meetsRequirement ? " is-ok" : "")} aria-live="polite">
          <strong>{count}</strong> selected
          {Number.isFinite(required) && ` · need at least ${required}`}
        </p>
      </div>

      <div className="qc-pool__toolbar">
        <input
          type="search"
          className="qc-pool__search"
          placeholder="Search questions…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          aria-label="Search questions"
        />
        <Select label="Filter by subject" value={subject} onChange={setSubject} options={SUBJECTS} />
        <Select
          label="Filter by difficulty"
          value={difficulty}
          onChange={setDifficulty}
          options={DIFFICULTIES}
        />
      </div>

      {error && (
        <p className="qc-field__error qc-pool__error" role="alert">
          {error}
        </p>
      )}

      <div className="qc-pool__table">
        <label className="qc-pool__row qc-pool__row--head">
          <input
            ref={selectAllRef}
            type="checkbox"
            checked={allVisibleSelected}
            onChange={toggleVisible}
            disabled={visible.length === 0}
          />
          <span>{allVisibleSelected ? "Clear shown questions" : "Select all shown questions"}</span>
          <span>Subject</span>
          <span>Difficulty</span>
        </label>

        {visible.length === 0 ? (
          <p className="qc-pool__empty">No questions match your filters.</p>
        ) : (
          visible.map((q) => (
            <label
              key={q.id}
              className={"qc-pool__row" + (selected.has(q.id) ? " is-selected" : "")}
            >
              <input type="checkbox" checked={selected.has(q.id)} onChange={() => toggleOne(q.id)} />
              <span className="qc-pool__text">{q.text}</span>
              <span className="qc-pool__muted">{q.subject}</span>
              <span className={`qc-badge is-${q.difficulty.toLowerCase()}`}>{q.difficulty}</span>
            </label>
          ))
        )}
      </div>
    </section>
  );
}