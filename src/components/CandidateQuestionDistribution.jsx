import { useEffect, useRef, useState } from 'react';
import { api } from '../api/client.js';
import { vnTime } from '../lib/time.js';
import { Status } from '../pages/Exams.jsx';
import '../styles/CandidateQuestionDistribution.css';

function scoreTone(val) {
  const n = Number(val);
  if (Number.isNaN(n)) return 'neutral';
  if (n >= 8) return 'good';
  if (n >= 5) return 'mid';
  return 'low';
}

function CandidateGradingCard({ examId, candidate, defaultOpen, onUpdated }) {
  const res = candidate.result;
  const [score, setScore] = useState(
    res?.lecturerScore != null ? String(res.lecturerScore) : res?.aiScore != null ? String(res.aiScore) : ''
  );
  const [comment, setComment] = useState(res?.lecturerComment || '');
  const [aiBusy, setAiBusy] = useState(false);
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

  const handleAiCheck = async () => {
    if (!examId) return;
    setAiBusy(true);
    setErr('');
    setMsg('');
    try {
      const r = await api.post(`/api/Exams/${examId}/candidates/${candidate.candidateId}/evaluate`);
      if (r?.aiScore != null && !candidate.result?.isFinalized) {
        setScore(String(r.aiScore));
      }
      setMsg('AI đã chấm và cập nhật nhận xét đối chiếu.');
      onUpdated?.();
    } catch (e) {
      setErr(e.message);
    } finally {
      setAiBusy(false);
    }
  };

  const handleSaveGrade = async e => {
    e.preventDefault();
    if (!examId) return;
    const num = Number(score);
    if (score === '' || Number.isNaN(num) || num < 0 || num > 10) {
      setErr('Vui lòng nhập điểm từ 0.0 đến 10.0.');
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
      setMsg('Đã lưu và chốt điểm chính thức.');
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
          <strong className="vr-cand-name">{candidate.studentName}</strong>
          <span className="wf-muted">{candidate.studentEmail} · {vnTime(candidate.scheduledTime)}</span>
        </div>
        <div className="vr-cand-summary__right">
          <Status value={candidate.status} />
          <span className={`vr-score-pill is-${scoreTone(res?.aiScore)}`}>
            AI gợi ý: <strong>{res?.aiScore != null ? `${Number(res.aiScore).toFixed(1)} / 10` : '—'}</strong>
          </span>
          <span className={`vr-score-pill ${res?.isFinalized ? 'is-finalized' : 'is-pending'}`}>
            {res?.isFinalized && res?.finalScore != null
              ? `✓ Điểm chốt: ${Number(res.finalScore).toFixed(1)} / 10`
              : 'Chờ GV chốt điểm'}
          </span>
        </div>
      </summary>

      <div className="vr-cand-body">
        <div className="vr-cand-toolbar">
          <span className="wf-muted">
            Đối chiếu câu trả lời của sinh viên (bên trái) với đáp án chuẩn (bên phải) để chấm điểm.
          </span>
          {examId && (
            <button
              type="button"
              className="wf-button wf-secondary"
              disabled={aiBusy || saveBusy}
              onClick={handleAiCheck}
            >
              {aiBusy ? '🤖 AI đang chấm điểm…' : '🤖 Dùng AI check điểm & đối chiếu'}
            </button>
          )}
        </div>

        <ol className="vr-qlist">
          {(candidate.questions || []).map((q, idx) => (
            <li key={q.examQuestionId} className="vr-qcard">
              <div className="vr-qcard__head">
                <div className="vr-qcard__qwrap">
                  <span className="vr-qcard__num">Câu hỏi {idx + 1}</span>
                  <h3 className="vr-qcard__title">{q.questionText}</h3>
                  {(q.durationSeconds != null || q.fluencyLabel) && (
                    <div className="vr-signals-row">
                      {q.durationSeconds != null && <span>⏱ Thời gian trả lời: <strong>{q.durationSeconds} giây</strong></span>}
                      {q.wordCount != null && <span>📝 Độ dài: <strong>{q.wordCount} từ</strong></span>}
                      {q.fluencyLabel && <span>🗣 Độ trôi chảy: <strong>{q.fluencyLabel}</strong></span>}
                    </div>
                  )}
                </div>

                {q.evaluation && (
                  <div className={`vr-qscore-big is-${scoreTone(q.evaluation.aiScore)}`}>
                    <span className="vr-qscore-big__label">Điểm AI gợi ý</span>
                    <strong className="vr-qscore-big__val">{Number(q.evaluation.aiScore).toFixed(1)} <span>/ 10</span></strong>
                  </div>
                )}
              </div>

              {/* Khung đối chiếu 2 cột song song: Sinh viên trả lời vs Đáp án thực tế */}
              <div className="vr-compare-grid">
                <div className="vr-compare-col vr-compare-col--student">
                  <div className="vr-compare-col__head">
                    <span>👨‍🎓 Câu trả lời của Sinh viên (Transcript)</span>
                  </div>
                  <div className="vr-compare-col__body">
                    {q.transcript ? (
                      <p className="vr-compare-text">{q.transcript}</p>
                    ) : (
                      <p className="vr-compare-empty">Sinh viên bỏ qua hoặc chưa có câu trả lời.</p>
                    )}

                    {q.followUps?.length > 0 && (
                      <div className="vr-followups">
                        {q.followUps.map((f, fi) => (
                          <div key={f.followUpId || fi} className="vr-followup-item">
                            <strong>↳ Câu hỏi phụ {fi + 1}: {f.questionText}</strong>
                            <p>{f.answerTranscript || <em className="wf-muted">Không có câu trả lời.</em>}</p>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>

                <div className="vr-compare-col vr-compare-col--expected">
                  <div className="vr-compare-col__head">
                    <span>🎯 Đáp án chuẩn / Đáp án thực tế cần đạt</span>
                  </div>
                  <div className="vr-compare-col__body">
                    {q.expectedAnswer ? (
                      <p className="vr-compare-text">{q.expectedAnswer}</p>
                    ) : (
                      <p className="vr-compare-empty">Câu hỏi này chưa có đáp án mẫu trong ngân hàng.</p>
                    )}
                  </div>
                </div>
              </div>

              {/* Đánh giá & Nhận xét đối chiếu từ AI */}
              {q.evaluation && (
                <div className="vr-ai-review">
                  <div className="vr-ai-review__summary">
                    <strong>🤖 Nhận xét đối chiếu của AI:</strong> {q.evaluation.feedback}
                  </div>
                  <div className="vr-ai-review__split">
                    <div className="vr-ai-point is-match">
                      <strong>✓ Ý sinh viên đã trả lời đúng / đạt được</strong>
                      <p>{q.evaluation.strengths || 'Chưa có ý đạt yêu cầu.'}</p>
                    </div>
                    <div className="vr-ai-point is-missing">
                      <strong>✗ Ý còn thiếu / lệch so với đáp án chuẩn</strong>
                      <p>{q.evaluation.missingPoints || q.evaluation.weaknesses || 'Đã đầy đủ các ý chính.'}</p>
                    </div>
                  </div>
                </div>
              )}
            </li>
          ))}
        </ol>

        {examId && (
          <form className="vr-grade-bar" onSubmit={handleSaveGrade}>
            <div className="vr-grade-bar__left">
              <div className="vr-grade-field">
                <label>Điểm chốt của Giảng viên (thang 10)</label>
                <div className="vr-grade-score-row">
                  <input
                    type="number"
                    step="0.1"
                    min="0"
                    max="10"
                    required
                    className="vr-score-input"
                    value={score}
                    onChange={e => setScore(e.target.value)}
                    placeholder="8.5"
                  />
                  {res?.aiScore != null && (
                    <button
                      type="button"
                      className="wf-button wf-secondary"
                      onClick={() => setScore(String(res.aiScore))}
                    >
                      Lấy điểm AI ({Number(res.aiScore).toFixed(1)})
                    </button>
                  )}
                </div>
              </div>

              <div className="vr-grade-field vr-grade-field--grow">
                <label>Nhận xét của Giảng viên dành cho Sinh viên</label>
                <input
                  type="text"
                  maxLength={2000}
                  value={comment}
                  onChange={e => setComment(e.target.value)}
                  placeholder="Nhập đánh giá hoặc nhận xét tổng kết cho sinh viên…"
                />
              </div>
            </div>

            <div className="vr-grade-bar__right">
              {err && <span className="vr-inline-msg is-err">{err}</span>}
              {msg && <span className="vr-inline-msg is-ok">{msg}</span>}
              <button type="submit" className="wf-button vr-btn-save" disabled={saveBusy || aiBusy}>
                {saveBusy ? 'Đang lưu…' : res?.isFinalized ? '✓ Cập nhật điểm đã chốt' : '✓ Xác nhận & Chốt điểm'}
              </button>
            </div>
          </form>
        )}
      </div>
    </details>
  );
}

export default function CandidateQuestionDistribution({ examId, candidates = [], mainCount = 0, onUpdated }) {
  const autoRanRef = useRef(false);
  const questions = candidates.flatMap(candidate => candidate.questions || []);

  useEffect(() => {
    if (!examId || autoRanRef.current) return;
    const ungraded = candidates.filter(c => c.status === 'Completed' && c.result?.aiScore == null);
    if (ungraded.length === 0) return;
    autoRanRef.current = true;
    (async () => {
      try {
        for (const c of ungraded) {
          await api.post(`/api/Exams/${examId}/candidates/${c.candidateId}/evaluate`);
        }
        onUpdated?.();
      } catch {
        // Ignore background error
      }
    })();
  }, [examId, candidates, onUpdated]);

  const completedCount = candidates.filter(c => c.status === 'Completed').length;
  const finalizedCount = candidates.filter(c => c.result?.isFinalized).length;

  return (
    <section className="wf-panel" style={{ marginTop: 24 }} aria-labelledby="distribution-title">
      <div className="wf-row" style={{ flexWrap: 'wrap' }}>
        <div>
          <h2 id="distribution-title" style={{ margin: '0 0 4px', fontSize: '22px' }}>
            Bảng đối chiếu đáp án & Chấm điểm thí sinh
          </h2>
          <p className="wf-muted" style={{ margin: 0 }}>
            {mainCount} câu chính / sinh viên · Đã hoàn thành: <strong>{completedCount}/{candidates.length}</strong> · Đã chốt điểm: <strong>{finalizedCount}/{candidates.length}</strong>
          </p>
        </div>
      </div>

      {questions.length === 0 ? (
        <p style={{ marginTop: 16 }}>Chưa có câu hỏi được phân.</p>
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
