import { useState } from "react";
import Sidebar from "../components/Sidebar.jsx";
import "../styles/Dashboard.css";

const PRACTICE_TOPICS = [
  { id: 1, title: "SOLID Principles & Architectural Patterns", course: "SWD392", questions: 5 },
  { id: 2, title: "Relational Normalization & SQL Indexing", course: "DBI202", questions: 4 },
  { id: 3, title: "REST vs gRPC & Microservices Communication", course: "PRN231", questions: 6 },
];

export default function AiInterview() {
  const [selectedTopic, setSelectedTopic] = useState(PRACTICE_TOPICS[0]);
  const [isSimulating, setIsSimulating] = useState(false);
  const [currentQuestionIndex, setCurrentQuestionIndex] = useState(0);
  const [isRecording, setIsRecording] = useState(false);

  const sampleQuestions = [
    "Explain the Dependency Inversion Principle (DIP) and how it decouples high-level policy from low-level implementation details.",
    "Can you provide a concrete scenario where violating the Single Responsibility Principle causes ripple effects in production maintenance?",
    "When would you recommend an Event-Driven architecture over synchronous HTTP REST for inter-service communication?",
  ];

  const handleStartPractice = () => {
    setIsSimulating(true);
    setCurrentQuestionIndex(0);
    setIsRecording(false);
  };

  const handleToggleRecord = () => {
    setIsRecording((prev) => !prev);
  };

  const handleNextQuestion = () => {
    if (currentQuestionIndex < sampleQuestions.length - 1) {
      setCurrentQuestionIndex((prev) => prev + 1);
      setIsRecording(false);
    } else {
      alert("Practice session completed! Your AI feedback report has been saved to your practice history.");
      setIsSimulating(false);
    }
  };

  return (
    <Sidebar
      title="AI Viva Practice Interview"
      subtitle="Practice oral defense answering techniques and follow-up prompts with an AI examiner."
    >
      {!isSimulating ? (
        <div style={{ display: "grid", gridTemplateColumns: "1.6fr 1fr", gap: "1.5rem" }}>
          <div className="panel">
            <h2 style={{ margin: "0 0 0.5rem", fontFamily: "var(--font-display)", fontSize: "1.2rem" }}>
              Select a Topic to Practice
            </h2>
            <p style={{ margin: "0 0 1.25rem", fontSize: "0.88rem", color: "var(--ink-text-dim)" }}>
              The AI viva examiner will ask main concepts followed by deep-dive follow-up questions based on your spoken answers.
            </p>

            <div style={{ display: "flex", flexDirection: "column", gap: "0.85rem" }}>
              {PRACTICE_TOPICS.map((topic) => (
                <div
                  key={topic.id}
                  onClick={() => setSelectedTopic(topic)}
                  style={{
                    padding: "1rem 1.25rem",
                    borderRadius: "8px",
                    border: selectedTopic.id === topic.id ? "2px solid var(--brass)" : "1px solid var(--parchment-line)",
                    background: selectedTopic.id === topic.id ? "#fffdf9" : "#fff",
                    cursor: "pointer",
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    transition: "all 0.15s ease",
                  }}
                >
                  <div>
                    <span
                      style={{
                        fontSize: "0.75rem",
                        fontWeight: 700,
                        color: "var(--brass-deep)",
                        background: "#fef3c7",
                        padding: "0.15rem 0.45rem",
                        borderRadius: "4px",
                      }}
                    >
                      {topic.course}
                    </span>
                    <h3 style={{ margin: "0.35rem 0 0.2rem", fontSize: "1rem", color: "var(--ink-text)" }}>
                      {topic.title}
                    </h3>
                  </div>
                  <span style={{ fontSize: "0.85rem", color: "var(--ink-text-dim)" }}>
                    {topic.questions} viva prompts
                  </span>
                </div>
              ))}
            </div>

            <div style={{ marginTop: "1.75rem" }}>
              <button
                type="button"
                className="btn-primary"
                style={{ padding: "0.75rem 1.5rem", fontSize: "0.95rem" }}
                onClick={handleStartPractice}
              >
                Start Practice Session
              </button>
            </div>
          </div>

          <div className="dash-side">
            <div className="panel">
              <h3 style={{ margin: "0 0 0.75rem", fontSize: "1.05rem", fontFamily: "var(--font-display)" }}>
                AI Evaluator Tips
              </h3>
              <ul style={{ margin: 0, paddingLeft: "1.2rem", fontSize: "0.85rem", color: "var(--ink-text-dim)", lineHeight: 1.6 }}>
                <li>Speak clearly and structure answers with: Concept definition → Real-world example → Tradeoffs.</li>
                <li>The AI listens for technical keywords, reasoning depth, and bloom taxonomy indicators.</li>
                <li>Expect 1–2 adaptive follow-up questions tailored directly to missing points in your initial answer.</li>
              </ul>
            </div>
          </div>
        </div>
      ) : (
        <div className="panel" style={{ maxWidth: "800px", margin: "0 auto", padding: "2rem" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1.5rem", borderBottom: "1px solid var(--parchment-line)", paddingBottom: "1rem" }}>
            <div>
              <span style={{ fontSize: "0.8rem", color: "var(--brass-deep)", fontWeight: 700 }}>
                QUESTION {currentQuestionIndex + 1} OF {sampleQuestions.length}
              </span>
              <h3 style={{ margin: "0.2rem 0 0", fontSize: "1.05rem" }}>
                Topic: {selectedTopic.title}
              </h3>
            </div>
            <button
              type="button"
              onClick={() => setIsSimulating(false)}
              style={{ background: "none", border: "1px solid var(--parchment-line)", padding: "0.4rem 0.8rem", borderRadius: "6px", cursor: "pointer", fontSize: "0.85rem" }}
            >
              Exit Practice
            </button>
          </div>

          <div style={{ background: "#f8fafc", padding: "1.5rem", borderRadius: "8px", border: "1px solid #e2e8f0", marginBottom: "2rem" }}>
            <span style={{ fontSize: "0.75rem", fontWeight: 700, color: "#64748b", textTransform: "uppercase" }}>
              AI Examiner Prompt
            </span>
            <p style={{ margin: "0.5rem 0 0", fontSize: "1.15rem", fontFamily: "var(--font-display)", color: "var(--ink-text)", lineHeight: 1.5 }}>
              "{sampleQuestions[currentQuestionIndex]}"
            </p>
          </div>

          <div style={{ textAlign: "center", margin: "2.5rem 0" }}>
            <button
              type="button"
              onClick={handleToggleRecord}
              style={{
                width: "80px",
                height: "80px",
                borderRadius: "50%",
                background: isRecording ? "var(--danger)" : "var(--ink)",
                color: "#fff",
                border: "none",
                cursor: "pointer",
                boxShadow: isRecording ? "0 0 0 10px rgba(182, 67, 46, 0.2)" : "0 4px 12px rgba(0,0,0,0.15)",
                transition: "all 0.2s ease",
              }}
            >
              {isRecording ? "Stop" : "Speak"}
            </button>
            <p style={{ marginTop: "1rem", fontSize: "0.85rem", color: isRecording ? "var(--danger)" : "var(--ink-text-dim)" }}>
              {isRecording ? "Listening to your answer... Click to stop." : "Click microphone button to record your response."}
            </p>
          </div>

          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <button
              type="button"
              className="btn-primary"
              onClick={handleNextQuestion}
              style={{ padding: "0.65rem 1.4rem" }}
            >
              {currentQuestionIndex < sampleQuestions.length - 1 ? "Submit & Next Question" : "Finish Practice"}
            </button>
          </div>
        </div>
      )}
    </Sidebar>
  );
}
