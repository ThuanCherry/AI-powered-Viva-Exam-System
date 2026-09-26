import { useMemo, useState } from "react";
import AppShell from "../components/Sidebar.jsx";
import "../styles/QuestionBank.css";

const SUBJECTS = ["All subjects", "Database Systems", "Software Engineering", "Network Security", "AI & Machine Learning"];
const DIFFICULTIES = ["All difficulty", "Easy", "Medium", "Hard"];
const TYPES = ["All types", "Multiple Choice", "Short Answer", "Essay", "Oral Prompt"];

const QUESTIONS = [
  {
    id: "q1",
    text: "Explain the differences between INNER JOIN and LEFT JOIN with an example.",
    subject: "Database Systems",
    type: "Short Answer",
    difficulty: "Medium",
    usedIn: 3,
    updated: "2 days ago",
  },
  {
    id: "q2",
    text: "What are the SOLID principles and why do they matter in software design?",
    subject: "Software Engineering",
    type: "Essay",
    difficulty: "Hard",
    usedIn: 5,
    updated: "1 week ago",
  },
  {
    id: "q3",
    text: "Describe how a SYN flood attack works and one way to mitigate it.",
    subject: "Network Security",
    type: "Short Answer",
    difficulty: "Hard",
    usedIn: 2,
    updated: "3 days ago",
  },
  {
    id: "q4",
    text: "What is the bias-variance tradeoff in machine learning?",
    subject: "AI & Machine Learning",
    type: "Oral Prompt",
    difficulty: "Medium",
    usedIn: 4,
    updated: "5 days ago",
  },
  {
    id: "q5",
    text: "Define database normalization and list the first three normal forms.",
    subject: "Database Systems",
    type: "Multiple Choice",
    difficulty: "Easy",
    usedIn: 6,
    updated: "1 day ago",
  },
  {
    id: "q6",
    text: "What is the difference between authentication and authorization?",
    subject: "Network Security",
    type: "Multiple Choice",
    difficulty: "Easy",
    usedIn: 7,
    updated: "4 days ago",
  },
  {
    id: "q7",
    text: "Walk through the steps of a typical Scrum sprint.",
    subject: "Software Engineering",
    type: "Oral Prompt",
    difficulty: "Easy",
    usedIn: 3,
    updated: "6 days ago",
  },
  {
    id: "q8",
    text: "Explain overfitting and describe two ways to reduce it.",
    subject: "AI & Machine Learning",
    type: "Short Answer",
    difficulty: "Medium",
    usedIn: 2,
    updated: "2 weeks ago",
  },
];

function SearchIcon() {
  return (
    <svg viewBox="0 0 20 20" width="16" height="16" aria-hidden="true">
      <circle cx="8.5" cy="8.5" r="5.5" fill="none" stroke="currentColor" strokeWidth="1.6" />
      <path d="M17 17l-4-4" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
    </svg>
  );
}

function ChevronIcon() {
  return (
    <svg viewBox="0 0 20 20" width="14" height="14" aria-hidden="true">
      <path
        d="M5 7.5l5 5 5-5"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function EditIcon() {
  return (
    <svg viewBox="0 0 20 20" width="16" height="16" aria-hidden="true">
      <path
        d="M13.3 3.3a1.8 1.8 0 0 1 2.5 2.5L6.5 15.1l-3.3.8.8-3.3 9.3-9.3Z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function TrashIcon() {
  return (
    <svg viewBox="0 0 20 20" width="16" height="16" aria-hidden="true">
      <path
        d="M4 6h12M8 6V4.5c0-.6.4-1 1-1h2c.6 0 1 .4 1 1V6M6 6l.6 9.4c0 .6.5 1.1 1.1 1.1h4.6c.6 0 1-.5 1.1-1.1L14 6"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function Select({ label, value, onChange, options }) {
  return (
    <label className="qb-select">
      <span className="sr-only">{label}</span>
      <select value={value} onChange={(e) => onChange(e.target.value)}>
        {options.map((opt) => (
          <option key={opt} value={opt}>
            {opt}
          </option>
        ))}
      </select>
      <ChevronIcon />
    </label>
  );
}

function DifficultyBadge({ level }) {
  const cls = { Easy: "is-easy", Medium: "is-medium", Hard: "is-hard" }[level] || "is-medium";
  return <span className={`qb-badge ${cls}`}>{level}</span>;
}

export default function QuestionBank() {
  const [search, setSearch] = useState("");
  const [subject, setSubject] = useState(SUBJECTS[0]);
  const [difficulty, setDifficulty] = useState(DIFFICULTIES[0]);
  const [type, setType] = useState(TYPES[0]);

  const filtered = useMemo(() => {
    return QUESTIONS.filter((q) => {
      const matchesSearch = q.text.toLowerCase().includes(search.trim().toLowerCase());
      const matchesSubject = subject === SUBJECTS[0] || q.subject === subject;
      const matchesDifficulty = difficulty === DIFFICULTIES[0] || q.difficulty === difficulty;
      const matchesType = type === TYPES[0] || q.type === type;
      return matchesSearch && matchesSubject && matchesDifficulty && matchesType;
    });
  }, [search, subject, difficulty, type]);

  const counts = useMemo(
    () => ({
      easy: QUESTIONS.filter((q) => q.difficulty === "Easy").length,
      medium: QUESTIONS.filter((q) => q.difficulty === "Medium").length,
      hard: QUESTIONS.filter((q) => q.difficulty === "Hard").length,
    }),
    []
  );

  return (
    <AppShell
      title="Question Bank"
      subtitle="Browse, filter, and maintain the questions used across your exams."
      actions={
        <button type="button" className="qb-add-btn">
          + Add question
        </button>
      }
    >
      <div className="qb-summary">
        <span>
          <strong>{QUESTIONS.length}</strong> questions total
        </span>
        <span className="qb-summary__sep" aria-hidden="true" />
        <span className="qb-summary__item is-easy">{counts.easy} Easy</span>
        <span className="qb-summary__item is-medium">{counts.medium} Medium</span>
        <span className="qb-summary__item is-hard">{counts.hard} Hard</span>
      </div>

      <div className="qb-toolbar">
        <div className="qb-search">
          <SearchIcon />
          <input
            type="search"
            placeholder="Search questions…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            aria-label="Search questions"
          />
        </div>
        <div className="qb-filters">
          <Select label="Filter by subject" value={subject} onChange={setSubject} options={SUBJECTS} />
          <Select label="Filter by difficulty" value={difficulty} onChange={setDifficulty} options={DIFFICULTIES} />
          <Select label="Filter by type" value={type} onChange={setType} options={TYPES} />
        </div>
      </div>

      <div className="qb-table">
        <div className="qb-table__row qb-table__row--head" aria-hidden="true">
          <span>Question</span>
          <span>Subject</span>
          <span>Type</span>
          <span>Difficulty</span>
          <span>Used in</span>
          <span>Updated</span>
          <span />
        </div>

        {filtered.length === 0 ? (
          <p className="qb-empty">No questions match your filters.</p>
        ) : (
          filtered.map((q) => (
            <div className="qb-table__row" key={q.id}>
              <span className="qb-table__text">{q.text}</span>
              <span className="qb-table__muted">{q.subject}</span>
              <span className="qb-type-tag">{q.type}</span>
              <DifficultyBadge level={q.difficulty} />
              <span className="qb-table__muted">{q.usedIn} exams</span>
              <span className="qb-table__muted">{q.updated}</span>
              <span className="qb-table__actions">
                <button type="button" aria-label={`Edit "${q.text}"`}>
                  <EditIcon />
                </button>
                <button type="button" aria-label={`Delete "${q.text}"`}>
                  <TrashIcon />
                </button>
              </span>
            </div>
          ))
        )}
      </div>

      <p className="qb-footnote">
        Showing {filtered.length} of {QUESTIONS.length} questions
      </p>
    </AppShell>
  );
}