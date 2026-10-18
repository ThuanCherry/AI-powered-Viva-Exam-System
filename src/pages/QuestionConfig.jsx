import { useEffect, useMemo, useState } from "react";
import { useParams, Link } from "react-router-dom";
import Sidebar from "../components/Sidebar.jsx";
import CandidateQuestionDistribution from "../components/CandidateQuestionDistribution.jsx";
import QuestionPool from "../components/QuestionPool.jsx";
import { getExam, assignCandidates, generateExamQuestions, updateExamStatus } from "../api/exams.js";
import { getQuestions } from "../api/questions.js";
import { getStudents } from "../api/users.js";
import { QUESTIONS as FALLBACK_QUESTIONS } from "../data/questions.js";
import "../styles/QuestionConfig.css";

// Giới hạn nhập liệu
const LIMITS = {
  mainCount: { min: 1, max: 20 },
  maxFollowUps: { min: 0, max: 5 },
};

const STRATEGIES = [
  {
    value: "RANDOM",
    title: "Random (Anti-Overlap)",
    description:
      "Each student gets a different draw from the pool. Consecutive candidates are guaranteed to have non-overlapping questions to prevent eavesdropping.",
  },
  {
    value: "ADAPTIVE",
    title: "Adaptive",
    description:
      "Candidates start with balanced medium-difficulty questions, while ensuring consecutive candidates see different initial questions.",
  },
];

const DEFAULT_CONFIG = { mainCount: "3", maxFollowUps: "2", strategy: "RANDOM" };

function toMinutes(value) {
  if (!value || typeof value !== "string") return Number.NaN;
  const [hours, minutes] = value.split(":").map(Number);
  if (!Number.isFinite(hours) || !Number.isFinite(minutes)) return Number.NaN;
  return hours * 60 + minutes;
}

function fromMinutes(totalMinutes) {
  const normalized = (totalMinutes % (24 * 60) + 24 * 60) % (24 * 60);
  const hours = Math.floor(normalized / 60);
  const mins = normalized % 60;
  return `${String(hours).padStart(2, "0")}:${String(mins).padStart(2, "0")}`;
}

function buildScheduleConflicts(schedule) {
  const entries = schedule
    .map((slot) => ({
      ...slot,
      startMinutes: toMinutes(slot.start),
      endMinutes: toMinutes(slot.end),
    }))
    .filter((slot) => Number.isFinite(slot.startMinutes) && Number.isFinite(slot.endMinutes));

  const conflicts = [];
  for (let i = 0; i < entries.length; i += 1) {
    for (let j = i + 1; j < entries.length; j += 1) {
      const a = entries[i];
      const b = entries[j];

      if (a.startMinutes < b.endMinutes && b.startMinutes < a.endMinutes) {
        conflicts.push({
          idA: a.id,
          idB: b.id,
          studentA: a.student,
          studentB: b.student,
        });
      }
    }
  }
  return conflicts;
}

function validateNumber(raw, { min, max }, noun) {
  if (raw === undefined || raw === null || String(raw).trim() === "" || !Number.isInteger(Number(raw))) {
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
  const hasValue = String(value).trim() !== "" && Number.isFinite(current);

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
      <p className="qc-field__hint">Algorithm for distributing questions to prevent duplicate questions between consecutive candidates.</p>

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

function StudentSchedulePanel({
  schedule,
  setSchedule,
  studentsList,
  selectedStudentId,
  setSelectedStudentId,
  durationMinutes = 20,
}) {
  const conflicts = useMemo(() => buildScheduleConflicts(schedule), [schedule]);
  const conflictById = useMemo(() => {
    const lookup = new Map();
    conflicts.forEach((conflict) => {
      lookup.set(conflict.idA, conflict);
      lookup.set(conflict.idB, conflict);
    });
    return lookup;
  }, [conflicts]);

  const updateSlot = (id, field, value) => {
    setSchedule((prev) =>
      prev.map((slot) => {
        if (slot.id !== id) return slot;
        const updated = { ...slot, [field]: value };
        if (field === "start" && value) {
          const startM = toMinutes(value);
          if (Number.isFinite(startM)) {
            updated.end = fromMinutes(startM + durationMinutes);
          }
        }
        return updated;
      })
    );
  };

  const addStudentSlot = () => {
    const student = studentsList.find((s) => String(s.userId) === String(selectedStudentId));
    if (!student) return;

    setSchedule((prev) => {
      let startTime = "09:00";
      if (prev.length > 0) {
        const lastEnd = prev[prev.length - 1].end;
        if (lastEnd && Number.isFinite(toMinutes(lastEnd))) {
          startTime = lastEnd;
        }
      }
      const startM = toMinutes(startTime);
      const endTime = fromMinutes(startM + durationMinutes);

      return [
        ...prev,
        {
          id: `slot-${Date.now()}-${prev.length + 1}`,
          studentId: student.userId,
          student: student.fullName || `Student #${student.userId}`,
          start: startTime,
          end: endTime,
        },
      ];
    });
  };

  const removeSlot = (id) => {
    setSchedule((prev) => prev.filter((slot) => slot.id !== id));
  };

  return (
    <section className="qc-card qc-scheduler" aria-labelledby="qc-schedule-title">
      <div className="qc-scheduler__header">
        <div>
          <h2 className="qc-card__title" id="qc-schedule-title">
            Candidate Scheduling & Slots
          </h2>
          <p className="qc-scheduler__subtitle">
            Assign students to specific viva timeslots ({durationMinutes} mins each). Order defines the sequential anti-overlap distribution.
          </p>
        </div>

        <div className="qc-scheduler__toolbar">
          <label className="qc-scheduler__select">
            <span className="sr-only">Choose student</span>
            <select
              value={selectedStudentId}
              onChange={(e) => setSelectedStudentId(e.target.value)}
            >
              {studentsList.map((student) => (
                <option key={student.userId} value={student.userId}>
                  {student.fullName} ({student.email})
                </option>
              ))}
            </select>
          </label>
          <button type="button" className="qc-btn qc-btn--ghost" onClick={addStudentSlot}>
            + Add candidate
          </button>
        </div>
      </div>

      <div className="qc-scheduler__summary" role="status">
        {conflicts.length > 0 ? (
          <span className="qc-scheduler__warning">{conflicts.length} overlap alert(s) detected.</span>
        ) : (
          <span className="qc-scheduler__ok">
            {schedule.length} candidate(s) scheduled. No time overlaps.
          </span>
        )}
      </div>

      {schedule.length === 0 ? (
        <p className="qc-scheduler__empty">No candidates scheduled yet. Select a student and click "+ Add candidate".</p>
      ) : (
        <div className="qc-scheduler__list">
          {schedule.map((slot, index) => {
            const startMinutes = toMinutes(slot.start);
            const endMinutes = toMinutes(slot.end);
            const invalidRange =
              Number.isFinite(startMinutes) && Number.isFinite(endMinutes) && startMinutes >= endMinutes;
            const hasConflict = Boolean(conflictById.get(slot.id));

            return (
              <div key={slot.id} className={`qc-scheduler__slot ${hasConflict ? "is-conflict" : ""}`}>
                <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                  <span style={{ fontWeight: 700, color: "#64748b", minWidth: "24px" }}>
                    #{index + 1}
                  </span>
                  <div className="qc-scheduler__student-wrap">
                    <label className="qc-scheduler__field">
                      <span>Candidate</span>
                      <input
                        type="text"
                        value={slot.student}
                        readOnly
                        style={{ background: "#f8fafc", cursor: "default" }}
                      />
                    </label>
                  </div>
                </div>

                <label className="qc-scheduler__field">
                  <span>Start Time</span>
                  <input
                    type="time"
                    value={slot.start}
                    onChange={(e) => updateSlot(slot.id, "start", e.target.value)}
                  />
                </label>

                <label className="qc-scheduler__field">
                  <span>End Time</span>
                  <input
                    type="time"
                    value={slot.end}
                    onChange={(e) => updateSlot(slot.id, "end", e.target.value)}
                  />
                </label>

                <div className="qc-scheduler__meta">
                  <span
                    className={
                      invalidRange
                        ? "qc-scheduler__chip qc-scheduler__chip--bad"
                        : hasConflict
                        ? "qc-scheduler__chip qc-scheduler__chip--warn"
                        : "qc-scheduler__chip qc-scheduler__chip--ok"
                    }
                  >
                    {invalidRange ? "Invalid" : hasConflict ? "Overlap" : "Scheduled"}
                  </span>
                  <button type="button" className="qc-scheduler__remove" onClick={() => removeSlot(slot.id)}>
                    Remove
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}

export default function QuestionConfig() {
  const { examId } = useParams();

  const [examState, setExamState] = useState({ examId: null, key: 0, exam: null, error: null });
  const [reloadKey, setReloadKey] = useState(0);

  const [availableQuestions, setAvailableQuestions] = useState([]);
  const [studentsList, setStudentsList] = useState([]);
  const [selectedStudentId, setSelectedStudentId] = useState("");

  const [config, setConfig] = useState(DEFAULT_CONFIG);
  const [selectedIds, setSelectedIds] = useState([]);
  const [schedule, setSchedule] = useState([]);
  const [touched, setTouched] = useState({});
  const [submitted, setSubmitted] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [saveSuccessMsg, setSaveSuccessMsg] = useState(null);
  const [saveError, setSaveError] = useState(null);

  // Load Exam
  useEffect(() => {
    if (!examId) return;
    const controller = new AbortController();
    getExam(examId, controller.signal)
      .then((data) => {
        setExamState({ examId, key: reloadKey, exam: data, error: null });

        if (data) {
          setConfig((prev) => ({
            ...prev,
            mainCount: String(data.mainQuestionCount ?? 3),
            maxFollowUps: String(data.maxFollowUpCount ?? 2),
          }));

          // Prefill schedule if exam already has candidates
          if (data.candidates && data.candidates.length > 0) {
            const mappedSchedule = data.candidates.map((c, i) => {
              const startStr = c.scheduledTime
                ? new Date(c.scheduledTime).toLocaleTimeString([], {
                    hour: "2-digit",
                    minute: "2-digit",
                    hour12: false,
                  })
                : fromMinutes(9 * 60 + i * (data.timePerStudent || 20));
              const startM = toMinutes(startStr);
              const endStr = fromMinutes(startM + (data.timePerStudent || 20));
              return {
                id: `cand-${c.candidateId}`,
                studentId: c.studentId,
                student: c.studentName || `Student #${c.studentId}`,
                start: startStr,
                end: endStr,
              };
            });
            setSchedule(mappedSchedule);
          }

          // Parse scope
          if (data.questionScopeJson) {
            try {
              const scope = JSON.parse(data.questionScopeJson);
              if (scope.selectedQuestionIds && Array.isArray(scope.selectedQuestionIds)) {
                setSelectedIds(scope.selectedQuestionIds.map(String));
              }
              if (scope.strategy) {
                setConfig((prev) => ({ ...prev, strategy: scope.strategy }));
              }
            } catch {
              // Ignore invalid scope JSON
            }
          }
        }
      })
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

  // Load Questions and Students
  useEffect(() => {
    if (!exam?.course?.courseId) return;

    // Fetch questions for course
    getQuestions({ courseId: exam.course.courseId })
      .then((qs) => {
        if (qs && qs.length > 0) {
          const formatted = qs.map((q) => ({
            id: String(q.questionId),
            text: q.questionText,
            subject: q.courseName || exam.course?.courseName || "Subject",
            difficulty: q.difficulty || "Medium",
            type: q.questionType || "Short Answer",
            bloomLevel: q.bloomLevel || "Understand",
          }));
          setAvailableQuestions(formatted);
          if (selectedIds.length === 0) {
            setSelectedIds(formatted.map((q) => q.id));
          }
        } else {
          // Fallback to demo questions
          setAvailableQuestions(FALLBACK_QUESTIONS);
          if (selectedIds.length === 0) {
            setSelectedIds(FALLBACK_QUESTIONS.map((q) => q.id));
          }
        }
      })
      .catch(() => {
        setAvailableQuestions(FALLBACK_QUESTIONS);
      });

    // Fetch students
    getStudents()
      .then((students) => {
        if (students && students.length > 0) {
          setStudentsList(students);
          if (!selectedStudentId) {
            setSelectedStudentId(String(students[0].userId));
          }
        } else {
          // Mock student list if DB empty
          const fallbackStudents = [
            { userId: 1, fullName: "Nguyen Minh Anh", email: "anh.nm@fpt.edu.vn" },
            { userId: 2, fullName: "Pham Gia Han", email: "han.pg@fpt.edu.vn" },
            { userId: 3, fullName: "Le Duc Thinh", email: "thinh.ld@fpt.edu.vn" },
            { userId: 4, fullName: "Vo Thi Kim Ngan", email: "ngan.vtk@fpt.edu.vn" },
            { userId: 5, fullName: "Tran Quoc Bao", email: "bao.tq@fpt.edu.vn" },
          ];
          setStudentsList(fallbackStudents);
          setSelectedStudentId("1");
        }
      })
      .catch(() => {});
  }, [exam?.course?.courseId]);

  const errors = useMemo(() => validate(config, selectedIds.length), [config, selectedIds]);
  const showError = (field) => (touched[field] || submitted ? errors[field] : null);

  const update = (field) => (value) => {
    setSaveSuccessMsg(null);
    setConfig((prev) => ({ ...prev, [field]: value }));
  };
  const updatePool = (ids) => {
    setSaveSuccessMsg(null);
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
    const chosen = new Set(selectedIds.map(String));
    return availableQuestions.filter((q) => chosen.has(String(q.id)));
  }, [availableQuestions, selectedIds]);

  const handleSave = async () => {
    setSubmitted(true);
    setSaveError(null);
    setSaveSuccessMsg(null);

    if (!isValid) return;

    if (schedule.length === 0) {
      setSaveError("Please add at least one candidate to the schedule.");
      return;
    }

    setIsSaving(true);
    try {
      // Step 1: Assign Candidates
      const examBaseDate = exam?.startTime ? new Date(exam.startTime) : new Date();
      const candidatesPayload = schedule.map((slot) => {
        const [h, m] = (slot.start || "09:00").split(":").map(Number);
        const schedTime = new Date(examBaseDate);
        schedTime.setHours(h, m, 0, 0);
        return {
          studentId: Number(slot.studentId) || 1,
          scheduledTime: schedTime.toISOString(),
        };
      });

      await assignCandidates(examId, candidatesPayload);

      // Step 2: Distribute Questions without duplicates between consecutive candidates
      const questionPayload = {
        selectedQuestionIds: selectedIds.map(Number).filter((n) => !Number.isNaN(n) && n > 0),
        strategy: config.strategy,
        mainQuestionCount: mainCount,
        maxFollowUpCount: followUps,
      };

      const result = await generateExamQuestions(examId, questionPayload);

      setSaveSuccessMsg(
        result?.message ||
          "Questions successfully distributed! Consecutive candidates have non-overlapping questions. Exam is now READY."
      );

      setReloadKey((k) => k + 1);
    } catch (err) {
      setSaveError(err.message || "Failed to distribute questions and save schedule.");
    } finally {
      setIsSaving(false);
    }
  };

  const handleReset = () => {
    setConfig(DEFAULT_CONFIG);
    setSelectedIds(availableQuestions.map((q) => q.id));
    setTouched({});
    setSubmitted(false);
    setSaveSuccessMsg(null);
    setSaveError(null);
  };

  const strategy = STRATEGIES.find((s) => s.value === config.strategy) || STRATEGIES[0];

  return (
    <Sidebar
      title="Question & Schedule Configuration"
      subtitle={
        exam?.examName
          ? `Configure questions and candidate slots for "${exam.examName}" (${exam.course?.courseCode || "Course"}).`
          : "Configure oral questions and candidate schedule."
      }
      actions={
        <div style={{ display: "flex", gap: "0.75rem", alignItems: "center" }}>
          <Link to="/exams" className="qc-btn qc-btn--ghost">
            &larr; Back to Exams
          </Link>
          <button type="button" className="qc-btn qc-btn--ghost" onClick={handleReset} disabled={isSaving}>
            Reset
          </button>
          <button
            type="button"
            className="qc-btn qc-btn--primary"
            onClick={handleSave}
            disabled={!exam || isSaving}
          >
            {isSaving ? "Distributing Questions..." : "Save & Distribute Questions"}
          </button>
        </div>
      }
    >
      {!examId && (
        <p className="qc-notice" role="alert">
          No exam selected. Select an exam from <Link to="/exams">Exam Management</Link>.
        </p>
      )}

      {loadingExam && (
        <p className="qc-notice qc-notice--info" role="status">
          Loading exam configuration...
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

      {saveSuccessMsg && (
        <div
          style={{
            padding: "1rem 1.25rem",
            background: "#ecfdf5",
            border: "1px solid #6ee7b7",
            color: "#065f46",
            borderRadius: "0.5rem",
            marginBottom: "1.5rem",
            fontWeight: 500,
          }}
        >
          &check; {saveSuccessMsg}
        </div>
      )}

      {saveError && (
        <div
          style={{
            padding: "1rem 1.25rem",
            background: "#fef2f2",
            border: "1px solid #fca5a5",
            color: "#b91c1c",
            borderRadius: "0.5rem",
            marginBottom: "1.5rem",
            fontWeight: 500,
          }}
        >
          &times; {saveError}
        </div>
      )}

      <div className="qc-layout">
        <div className="qc-main">
          {/* Status Alert Banner */}
          {exam && (
            <div
              style={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                padding: "0.875rem 1.25rem",
                background: "#f8fafc",
                border: "1px solid #e2e8f0",
                borderRadius: "0.75rem",
                marginBottom: "1.25rem",
              }}
            >
              <div>
                <strong>Current State: </strong>
                <span className={`badge-status is-${(exam.status || "draft").toLowerCase()}`}>
                  {exam.status}
                </span>
                <span style={{ marginLeft: "1rem", color: "#64748b", fontSize: "0.9rem" }}>
                  Assigned questions: {exam.assignedQuestionsCount ?? 0}
                </span>
              </div>
              <span style={{ fontSize: "0.85rem", color: "#64748b" }}>
                Window: {new Date(exam.startTime).toLocaleDateString()} &bull; {exam.timePerStudent} mins / candidate
              </span>
            </div>
          )}

          {/* Section 1: Question Settings */}
          <section className="qc-card" aria-labelledby="qc-params-title">
            <h2 className="qc-card__title" id="qc-params-title">
              Viva Question Settings
            </h2>

            <NumberField
              id="qc-main-count"
              label="Main questions"
              hint={`Primary oral questions asked per student (${LIMITS.mainCount.min}–${LIMITS.mainCount.max}).`}
              value={config.mainCount}
              limits={LIMITS.mainCount}
              error={showError("mainCount")}
              onChange={update("mainCount")}
              onBlur={markTouched("mainCount")}
            />

            <NumberField
              id="qc-follow-ups"
              label="Max follow-up (deep-dive) questions"
              hint={`Follow-up prompts the AI / Panel may ask per question (${LIMITS.maxFollowUps.min}–${LIMITS.maxFollowUps.max}).`}
              value={config.maxFollowUps}
              limits={LIMITS.maxFollowUps}
              error={showError("maxFollowUps")}
              onChange={update("maxFollowUps")}
              onBlur={markTouched("maxFollowUps")}
            />

            <StrategyPicker value={config.strategy} onChange={update("strategy")} />
          </section>

          {/* Section 2: Question Pool */}
          <QuestionPool
            questions={availableQuestions}
            selectedIds={selectedIds}
            onChange={updatePool}
            required={Number.isFinite(mainCount) && !errors.mainCount ? mainCount : NaN}
            error={showError("pool")}
          />

          {/* Step 3: Candidate Scheduling & Slots */}
          <StudentSchedulePanel
            schedule={schedule}
            setSchedule={setSchedule}
            studentsList={studentsList}
            selectedStudentId={selectedStudentId}
            setSelectedStudentId={setSelectedStudentId}
            durationMinutes={exam?.timePerStudent || 20}
          />

          {/* Step 4: Unified Candidate Question Distribution & Anti-Overlap Verification */}
          <CandidateQuestionDistribution
            candidates={exam?.candidates || []}
            schedule={schedule}
            pool={selectedQuestions}
            mainCount={paramsValid ? mainCount : 0}
            maxFollowUps={paramsValid ? followUps : 0}
            strategy={config.strategy}
            isLocked={Boolean(exam?.status === "Ready" || exam?.status === "InProgress")}
            onRegenerate={handleSave}
            isRegenerating={isSaving}
          />
        </div>

        {/* Sidebar Summary */}
        <aside className="qc-card qc-summary" aria-labelledby="qc-summary-title">
          <h2 className="qc-card__title" id="qc-summary-title">
            Configuration Summary
          </h2>

          {paramsValid ? (
            <>
              <p className="qc-summary__figure">
                {mainCount}
                <span> main {mainCount === 1 ? "question" : "questions"}</span>
              </p>
              <p className="qc-summary__line">
                {followUps === 0
                  ? "No follow-up questions."
                  : `Up to ${maxFollowUpTotal} follow-ups (${followUps} per main question).`}
              </p>
              <p className="qc-summary__line">
                At most <strong>{mainCount + maxFollowUpTotal}</strong> questions total.
              </p>
              <p className="qc-summary__line">
                Distribution: <strong>{strategy.title}</strong>
              </p>
              <p className="qc-summary__line">
                Pool selected: <strong>{selectedIds.length}</strong> questions
              </p>
              <p className="qc-summary__line">
                Candidates scheduled: <strong>{schedule.length}</strong> students
              </p>

              {selectedIds.length < mainCount * 2 && selectedIds.length >= mainCount && (
                <p className="qc-summary__note" style={{ background: "#fffbeb", color: "#b45309" }}>
                  Tip: A pool of at least {mainCount * 2} questions is recommended to ensure 100% zero-overlap between consecutive candidates.
                </p>
              )}
            </>
          ) : (
            <p className="qc-summary__line">Fix the highlighted fields to see the totals.</p>
          )}

          <div style={{ marginTop: "1.5rem" }}>
            <button
              type="button"
              className="qc-btn qc-btn--primary"
              style={{ width: "100%", justifyContent: "center" }}
              onClick={handleSave}
              disabled={!exam || isSaving}
            >
              {isSaving ? "Distributing..." : "Save & Generate Sets"}
            </button>
          </div>
        </aside>
      </div>
    </Sidebar>
  );
}