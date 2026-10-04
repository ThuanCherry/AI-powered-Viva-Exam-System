import { useMemo, useState } from "react";
import { useParams } from "react-router-dom";
import Sidebar from "../components/Sidebar.jsx";
import "../styles/QuestionConfig.css";

// Giới hạn nhập liệu. Chỉnh ở đây nếu đề bài / backend quy định khác.
const LIMITS = {
  mainCount: { min: 1, max: 20 },
  maxFollowUps: { min: 0, max: 3 },
};

const STRATEGIES = [
  {
    value: "RANDOM",
    title: "Random",
    description:
      "Each student gets a different random draw from the question pool, so consecutive candidates rarely see the same questions.",
  },
  {
    value: "ADAPTIVE",
    title: "Adaptive",
    description:
      "The next main question is chosen from the pool based on how well the student answered the previous one.",
  },
];

const DEFAULT_CONFIG = { mainCount: "5", maxFollowUps: "2", strategy: "RANDOM" };

// TODO: thay bằng dữ liệu thật từ API (GET /api/exams/{examId}) khi backend sẵn sàng.
const MOCK_EXAM = { id: "demo", title: "Database Systems — Final Viva" };

function validateNumber(raw, { min, max }, noun) {
  if (raw.trim() === "" || !Number.isInteger(Number(raw))) {
    return "Enter a whole number.";
  }
  const value = Number(raw);
  if (value < min || value > max) {
    return `Choose between ${min} and ${max} ${noun}.`;
  }
  return null;
}

function validate(config) {
  const errors = {};
  const mainError = validateNumber(config.mainCount, LIMITS.mainCount, "main questions");
  const followError = validateNumber(config.maxFollowUps, LIMITS.maxFollowUps, "follow-ups");
  if (mainError) errors.mainCount = mainError;
  if (followError) errors.maxFollowUps = followError;
  return errors;
}

function clamp(value, { min, max }) {
  return Math.min(max, Math.max(min, value));
}

function NumberField({ id, label, hint, value, limits, error, onChange, onBlur }) {
  const current = Number(value);
  const hasValue = value.trim() !== "" && Number.isFinite(current);

  const step = (delta) => {
    const base = hasValue ? current : limits.min;
    onChange(String(clamp(base + delta, limits)));
  };

  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;

  return (
    <div className="qc-field">
      <label className="qc-field__label" htmlFor={id}>
        {label}
      </label>
      <p className="qc-field__hint" id={hintId}>
        {hint}
      </p>

      <div className={"qc-stepper" + (error ? " has-error" : "")}>
        <button
          type="button"
          onClick={() => step(-1)}
          disabled={hasValue && current <= limits.min}
          aria-label={`Decrease ${label.toLowerCase()}`}
        >
          −
        </button>
        <input
          id={id}
          type="number"
          inputMode="numeric"
          min={limits.min}
          max={limits.max}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          onBlur={onBlur}
          aria-invalid={error ? "true" : undefined}
          aria-describedby={error ? `${hintId} ${errorId}` : hintId}
        />
        <button
          type="button"
          onClick={() => step(1)}
          disabled={hasValue && current >= limits.max}
          aria-label={`Increase ${label.toLowerCase()}`}
        >
          +
        </button>
      </div>

      {error && (
        <p className="qc-field__error" id={errorId}>
          {error}
        </p>
      )}
    </div>
  );
}

function StrategyPicker({ value, onChange }) {
  return (
    <fieldset className="qc-field qc-strategy">
      <legend className="qc-field__label">Question strategy</legend>
      <p className="qc-field__hint">How the system picks questions for each student.</p>

      <div className="qc-strategy__options">
        {STRATEGIES.map((option) => {
          const selected = value === option.value;
          return (
            <label
              key={option.value}
              className={"qc-strategy__option" + (selected ? " is-selected" : "")}
            >
              <input
                type="radio"
                name="strategy"
                value={option.value}
                checked={selected}
                onChange={() => onChange(option.value)}
              />
              <span className="qc-strategy__title">{option.title}</span>
              <span className="qc-strategy__desc">{option.description}</span>
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}

export default function QuestionConfig() {
  const { examId } = useParams();
  const exam = { ...MOCK_EXAM, id: examId ?? MOCK_EXAM.id };

  const [config, setConfig] = useState(DEFAULT_CONFIG);
  const [touched, setTouched] = useState({});
  const [submitted, setSubmitted] = useState(false);
  const [savedAt, setSavedAt] = useState(null);

  const errors = useMemo(() => validate(config), [config]);
  const showError = (field) => (touched[field] || submitted ? errors[field] : null);

  const update = (field) => (value) => {
    setSavedAt(null);
    setConfig((prev) => ({ ...prev, [field]: value }));
  };
  const markTouched = (field) => () => setTouched((prev) => ({ ...prev, [field]: true }));

  const isValid = Object.keys(errors).length === 0;
  const mainCount = Number(config.mainCount);
  const followUps = Number(config.maxFollowUps);
  const maxFollowUpTotal = mainCount * followUps;

  const handleSave = () => {
    setSubmitted(true);
    if (!isValid) return;

    const payload = {
      examId: exam.id,
      mainQuestionCount: mainCount,
      maxFollowUpPerQuestion: followUps,
      strategy: config.strategy,
    };
    // TODO: gọi API lưu cấu hình (ví dụ PUT /api/exams/{examId}/question-config)
    console.log("Question config payload:", payload);
    setSavedAt(new Date());
  };

  const handleReset = () => {
    setConfig(DEFAULT_CONFIG);
    setTouched({});
    setSubmitted(false);
    setSavedAt(null);
  };

  const strategy = STRATEGIES.find((s) => s.value === config.strategy);

  return (
    <Sidebar
      title="Question Configuration"
      subtitle={`Set how many questions each student gets in ${exam.title}.`}
      actions={
        <>
          <button type="button" className="qc-btn qc-btn--ghost" onClick={handleReset}>
            Reset
          </button>
          <button type="button" className="qc-btn qc-btn--primary" onClick={handleSave}>
            Save configuration
          </button>
        </>
      }
    >
      <div className="qc-layout">
        <section className="qc-card" aria-labelledby="qc-params-title">
          <h2 className="qc-card__title" id="qc-params-title">
            Question settings
          </h2>

          <NumberField
            id="qc-main-count"
            label="Main questions"
            hint={`Questions every student is asked (${LIMITS.mainCount.min}–${LIMITS.mainCount.max}).`}
            value={config.mainCount}
            limits={LIMITS.mainCount}
            error={showError("mainCount")}
            onChange={update("mainCount")}
            onBlur={markTouched("mainCount")}
          />

          <NumberField
            id="qc-follow-ups"
            label="Follow-ups per question"
            hint={`Most follow-up questions the AI may ask after one answer (${LIMITS.maxFollowUps.min}–${LIMITS.maxFollowUps.max}).`}
            value={config.maxFollowUps}
            limits={LIMITS.maxFollowUps}
            error={showError("maxFollowUps")}
            onChange={update("maxFollowUps")}
            onBlur={markTouched("maxFollowUps")}
          />

          <StrategyPicker value={config.strategy} onChange={update("strategy")} />
        </section>

        <aside className="qc-card qc-summary" aria-labelledby="qc-summary-title">
          <h2 className="qc-card__title" id="qc-summary-title">
            Per student
          </h2>

          {isValid ? (
            <>
              <p className="qc-summary__figure">
                {mainCount}
                <span> main {mainCount === 1 ? "question" : "questions"}</span>
              </p>
              <p className="qc-summary__line">
                {followUps === 0
                  ? "No follow-ups."
                  : `Up to ${maxFollowUpTotal} follow-ups (${followUps} per question).`}
              </p>
              <p className="qc-summary__line">
                At most <strong>{mainCount + maxFollowUpTotal}</strong> questions in total.
              </p>
              <p className="qc-summary__line">
                Strategy: <strong>{strategy.title}</strong>
              </p>
            </>
          ) : (
            <p className="qc-summary__line">Fix the highlighted fields to see the totals.</p>
          )}

          <p className="qc-summary__status" role="status">
            {savedAt ? `Configuration saved at ${savedAt.toLocaleTimeString()}.` : ""}
          </p>
        </aside>
      </div>
    </Sidebar>
  );
}