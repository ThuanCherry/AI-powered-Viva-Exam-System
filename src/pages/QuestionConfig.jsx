import { useEffect, useMemo, useState } from "react";
import { useParams } from "react-router-dom";
import Sidebar from "../components/Sidebar.jsx";
import DistributionPreview from "../components/DistributionPreview.jsx";
import QuestionPool from "../components/QuestionPool.jsx";
import { getExam } from "../api/exams.js";
import { QUESTIONS } from "../data/questions.js";
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

function validate(config, poolSize) {
  const errors = {};
  const mainError = validateNumber(config.mainCount, LIMITS.mainCount, "main questions");
  const followError = validateNumber(config.maxFollowUps, LIMITS.maxFollowUps, "follow-ups");
  if (mainError) errors.mainCount = mainError;
  if (followError) errors.maxFollowUps = followError;

  // Chỉ kiểm tra pool khi số câu chính hợp lệ.
  if (!mainError) {
    const needed = Number(config.mainCount);
    if (poolSize < needed) {
      errors.pool =
        poolSize === 0
          ? `Select at least ${needed} questions for the pool.`
          : `The pool has ${poolSize} of the ${needed} questions needed. Select ${needed - poolSize} more.`;
    }
  }
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

  // Tải exam thật từ backend. Trạng thái gắn kèm examId + reloadKey để không phải setState đồng bộ trong effect.
  const [examState, setExamState] = useState({ examId: null, key: 0, exam: null, error: null });
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (!examId) return undefined;
    const controller = new AbortController();
    getExam(examId, controller.signal)
      .then((data) => setExamState({ examId, key: reloadKey, exam: data, error: null }))
      .catch((error) => {
        if (error.name === "AbortError") return;
        setExamState({ examId, key: reloadKey, exam: null, error: error.message });
      });
    return () => controller.abort();
  }, [examId, reloadKey]);

  const examLoaded = examState.examId === examId && examState.key === reloadKey;
  const loadingExam = Boolean(examId) && !examLoaded;
  const exam = examLoaded ? examState.exam : null;
  const examError = examLoaded ? examState.error : null;

  const [config, setConfig] = useState(DEFAULT_CONFIG);
  const [selectedIds, setSelectedIds] = useState([]);
  const [touched, setTouched] = useState({});
  const [submitted, setSubmitted] = useState(false);
  const [savedAt, setSavedAt] = useState(null);

  const errors = useMemo(() => validate(config, selectedIds.length), [config, selectedIds]);
  const showError = (field) => (touched[field] || submitted ? errors[field] : null);

  const update = (field) => (value) => {
    setSavedAt(null);
    setConfig((prev) => ({ ...prev, [field]: value }));
  };
  const updatePool = (ids) => {
    setSavedAt(null);
    setTouched((prev) => ({ ...prev, pool: true }));
    setSelectedIds(ids);
  };
  const markTouched = (field) => () => setTouched((prev) => ({ ...prev, [field]: true }));

  const isValid = Object.keys(errors).length === 0;
  const paramsValid = !errors.mainCount && !errors.maxFollowUps;
  const mainCount = Number(config.mainCount);
  const followUps = Number(config.maxFollowUps);
  const maxFollowUpTotal = mainCount * followUps;
  const selectedQuestions = useMemo(() => {
    const chosen = new Set(selectedIds);
    return QUESTIONS.filter((q) => chosen.has(q.id));
  }, [selectedIds]);

  const handleSave = () => {
    setSubmitted(true);
    if (!isValid) return;

    const payload = {
      examId: Number(examId),
      mainQuestionCount: mainCount,
      maxFollowUpPerQuestion: followUps,
      strategy: config.strategy,
      selectedQuestionIds: selectedIds,
    };
    // TODO: backend chưa có endpoint lưu cấu hình câu hỏi (xem phần "Cần backend bổ sung").
    console.log("Question config payload:", payload);
    setSavedAt(new Date());
  };

  const handleReset = () => {
    setConfig(DEFAULT_CONFIG);
    setSelectedIds([]);
    setTouched({});
    setSubmitted(false);
    setSavedAt(null);
  };

  const strategy = STRATEGIES.find((s) => s.value === config.strategy);

  return (
    <Sidebar
      title="Question Configuration"
      subtitle={
        exam?.title
          ? `Set how many questions each student gets in ${exam.title}.`
          : "Set how many questions each student gets in an exam."
      }
      actions={
        <>
          <button type="button" className="qc-btn qc-btn--ghost" onClick={handleReset}>
            Reset
          </button>
          <button
            type="button"
            className="qc-btn qc-btn--primary"
            onClick={handleSave}
            disabled={!exam}
          >
            Save configuration
          </button>
        </>
      }
    >
      {!examId && (
        <p className="qc-notice" role="alert">
          No exam selected. Open this page from an exam, for example /exams/1/config.
        </p>
      )}
      {loadingExam && (
        <p className="qc-notice qc-notice--info" role="status">
          Loading exam…
        </p>
      )}
      {examError && (
        <p className="qc-notice" role="alert">
          {examError}{" "}
          <button type="button" className="qc-notice__retry" onClick={() => setReloadKey((k) => k + 1)}>
            Try again
          </button>
        </p>
      )}

      <div className="qc-layout">
        <div className="qc-main">
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

        <QuestionPool
          questions={QUESTIONS}
          selectedIds={selectedIds}
          onChange={updatePool}
          required={Number.isFinite(mainCount) && !errors.mainCount ? mainCount : NaN}
          error={showError("pool")}
        />

        <DistributionPreview
          pool={selectedQuestions}
          mainCount={paramsValid ? mainCount : NaN}
          maxFollowUps={paramsValid ? followUps : 0}
          strategy={config.strategy}
        />
        </div>

        <aside className="qc-card qc-summary" aria-labelledby="qc-summary-title">
          <h2 className="qc-card__title" id="qc-summary-title">
            Per student
          </h2>

          {paramsValid ? (
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
              <p className="qc-summary__line">
                Pool: <strong>{selectedIds.length}</strong>{" "}
                {selectedIds.length === 1 ? "question" : "questions"}
              </p>
              {config.strategy === "RANDOM" && selectedIds.length === mainCount && (
                <p className="qc-summary__note">
                  The pool is the same size as the main questions, so every student will get the
                  same set. Add more questions for a different draw per student.
                </p>
              )}
            </>
          ) : (
            <p className="qc-summary__line">Fix the highlighted fields to see the totals.</p>
          )}

          <p className="qc-summary__status" role="status">
            {savedAt
              ? `Configuration is valid (${savedAt.toLocaleTimeString()}). The server cannot store it yet.`
              : ""}
          </p>
        </aside>
      </div>
    </Sidebar>
  );
}