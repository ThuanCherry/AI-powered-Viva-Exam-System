import { useEffect, useRef, useState } from 'react';
import { api } from '../api/client.js';
import { vnTime } from '../lib/time.js';
import { Status } from '../pages/Exams.jsx';
import '../styles/CandidateQuestionDistribution.css';

function CandidateGradingCard({ examId, candidate, defaultOpen, onUpdated }) {
  const res = candidate.result;
  const [score, setScore] = useState(
    res?.lecturerScore != null ? String(res.lecturerScore) : res?.aiScore != null ? String(res.aiScore) : ''
  );
  const [comment, setComment] = useState(res?.lecturerComment || '');
  const [saveBusy, setSaveBusy] = useState(false);
  const [msg, setMsg] = useState('');
  const [err, setErr] = useState('');

  useEffect(() => {
    setScore(
      candidate.result?.lecturerScore != null
        ? String(candidate.result.lecturerScore)
        : candidate.result?.aiScore != null
        ? String(candidate.result.aiScore)
        : ''
    );
    setComment(candidate.result?.lecturerComment || '');
  }, [candidate.result?.lecturerScore, candidate.result?.aiScore, candidate.result?.lecturerComment]);

  const handleSaveGrade = async e => {
    e.preventDefault();
    if (!examId) return;
    const num = Number(score);
    if (score === '' || Number.isNaN(num) || num < 0 || num > 10) {
      setErr('Nhập điểm từ 0.0 đến 10.0.');
      return;
    }
    setSaveBusy(true);
    setErr('');
    setMsg('');
    try {
      await api.put(`/api/Exams/${examId}/candidates/${candidate.candidateId}/grade`, {
        lecturerScore: num,
        lecturerComment: comment
      });
      setMsg('Đã lưu điểm chính thức.');
      onUpdated?.();
    } catch (e) {
      setErr(e.message);
    } finally {
      setSaveBusy(false);
    }
  };

  return (
    <details className="vr-cand-card" open={defaultOpen}>
      <summary className="vr-cand-summary">
        <div className="vr-cand-summary__left">
          <strong>{candidate.studentName}</strong>
          <span className="wf-muted">{vnTime(candidate.scheduledTime)}</span>
        </div>
        <div className="vr-cand-summary__right">
          <Status value={candidate.status} />
          <span className="vr-badge vr-badge--ai">
            AI gợi ý: {res?.aiScore != null ? `${Number(res.aiScore).toFixed(1)}` : '—'}
          </span>
          <span className={`vr-badge ${res?.isFinalized ? 'vr-badge--final' : 'vr-badge--pending'}`}>
            {res?.isFinalized && res?.finalScore != null
              ? `Điểm chốt: ${Number(res.finalScore).toFixed(1)}/10`
              : 'Chờ duyệt'}
          </span>
        </div>
      </summary>

      <div className="vr-cand-body">
        <ol className="vr-qlist">
          {(candidate.questions || []).map((q, idx) => (
            <li key={q.examQuestionId} className="vr-qcard">
              <div className="vr-qcard__head">
                <strong className="vr-qcard__title">
                  <span className="vr-qcard__num">Câu {idx + 1}.</span> {q.questionText}
                </strong>
                {q.evaluation && (
                  <span className="vr-qscore">{Number(q.evaluation.aiScore).toFixed(1)} / 10</span>
                )}
              </div>

              <div className="vr-ans-block">
                <p className="vr-ans-text">
                  {q.transcript ? q.transcript : <em className="wf-muted">Chưa có câu trả lời.</em>}
                </p>
                {(q.durationSeconds != null || q.fluencyLabel) && (
                  <small className="vr-meta-line">
                    {q.durationSeconds != null && `Thời gian: ${q.durationSeconds}s`}
                    {q.fluencyLabel && ` · Độ trôi chảy: ${q.fluencyLabel}`}
                    {q.expectedAnswer && ` · Đáp án mẫu: ${q.expectedAnswer}`}
                  </small>
                )}
              </div>

              {q.followUps?.length > 0 && (
                <div className="vr-followups">
                  {q.followUps.map((f, fi) => (
                    <div key={f.followUpId || fi} className="vr-followup-item">
                      <strong>↳ Hỏi thêm: {f.questionText}</strong>
                      <p>{f.answerTranscript || <em className="wf-muted">Không trả lời.</em>}</p>
                    </div>
                  ))}
                </div>
              )}

              {q.evaluation && (
                <div className="vr-eval-compact">
                  <p className="vr-eval-compact__main">
                    <strong>Nhận xét AI:</strong> {q.evaluation.feedback}
                  </p>
                  <div className="vr-eval-compact__points">
                    {q.evaluation.strengths && <span><strong>✓ Đạt:</strong> {q.evaluation.strengths}</span>}
                    {q.evaluation.missingPoints && <span><strong>△ Thiếu:</strong> {q.evaluation.missingPoints}</span>}
                  </div>
                </div>
              )}
            </li>
          ))}
        </ol>

        {examId && (
          <form className="vr-grade-inline" onSubmit={handleSaveGrade}>
            <label className="vr-grade-inline__score">
              <span>Điểm chốt (0–10)</span>
              <input
                type="number"
                step="0.1"
                min="0"
                max="10"
                required
                value={score}
                onChange={e => setScore(e.target.value)}
                placeholder="8.5"
              />
            </label>
            <label className="vr-grade-inline__comment">
              <span>Nhận xét của giảng viên</span>
              <input
                type="text"
                maxLength={2000}
                value={comment}
                onChange={e => setComment(e.target.value)}
                placeholder="Nhập nhận xét ngắn gọn cho sinh viên (tùy chọn)…"
              />
            </label>
            <button type="submit" className="wf-button" disabled={saveBusy}>
              {saveBusy ? 'Đang lưu…' : res?.isFinalized ? 'Cập nhật điểm' : 'Chốt điểm'}
            </button>
            {err && <span className="vr-inline-msg is-err">{err}</span>}
            {msg && <span className="vr-inline-msg is-ok">{msg}</span>}
          </form>
        )}
      </div>
    </details>
  );
}

export default function CandidateQuestionDistribution({ examId, candidates = [], mainCount = 0, onUpdated }) {
  const autoRanRef = useRef(false);
  const questions = candidates.flatMap(candidate => candidate.questions || []);

  // Tự động chạy chấm AI ngầm cho các bài đã hoàn thành mà chưa có điểm AI
  useEffect(() => {
    if (!examId || autoRanRef.current) return;
    const ungraded = candidates.filter(
      c => c.status === 'Completed' && c.result?.aiScore == null
    );
    if (ungraded.length === 0) return;
    autoRanRef.current = true;
    (async () => {
      try {
        for (const c of ungraded) {
          await api.post(`/api/Exams/${examId}/candidates/${c.candidateId}/evaluate`);
        }
        onUpdated?.();
      } catch {
        // Ignore background auto-eval error
      }
    })();
  }, [examId, candidates, onUpdated]);

  const completedCount = candidates.filter(c => c.status === 'Completed').length;
  const finalizedCount = candidates.filter(c => c.result?.isFinalized).length;

  return (
    <section className="wf-panel" style={{ marginTop: 20 }} aria-labelledby="distribution-title">
      <div className="wf-row" style={{ flexWrap: 'wrap' }}>
        <h2 id="distribution-title" style={{ margin: 0 }}>Bảng chấm điểm & Câu hỏi thí sinh</h2>
        <span className="wf-muted">
          {mainCount} câu/SV · Hoàn thành: <strong>{completedCount}/{candidates.length}</strong> · Đã chốt điểm: <strong>{finalizedCount}/{candidates.length}</strong>
        </span>
      </div>

      {questions.length === 0 ? (
        <p style={{ marginTop: 14 }}>Chưa có câu hỏi được phân.</p>
      ) : (
        <div className="vr-cand-list">
          {candidates.map((candidate, idx) => (
            <CandidateGradingCard
              key={candidate.candidateId || candidate.studentId}
              examId={examId}
              candidate={candidate}
              defaultOpen={candidate.status === 'Completed' || idx === 0}
              onUpdated={onUpdated}
            />
          ))}
        </div>
      )}
    </section>
  );
}
