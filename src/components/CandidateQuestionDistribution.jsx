import { useEffect, useState } from 'react';
import { api } from '../api/client.js';
import { vnTime } from '../lib/time.js';
import { Status } from '../pages/Exams.jsx';
import '../styles/CandidateQuestionDistribution.css';

const diffLabels = { Easy: 'Dễ', Medium: 'Trung bình', Hard: 'Khó' };

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

  const handleAiEvaluate = async () => {
    if (!examId) return;
    setAiBusy(true);
    setErr('');
    setMsg('');
    try {
      const r = await api.post(`/api/Exams/${examId}/candidates/${candidate.candidateId}/evaluate`);
      if (r?.aiScore != null && (score === '' || !candidate.result?.isFinalized)) {
        setScore(String(r.aiScore));
      }
      setMsg('Đã cập nhật điểm gợi ý và nhận xét từ AI.');
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
      setErr('Vui lòng nhập điểm hợp lệ từ 0.0 đến 10.0.');
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
      setMsg('Đã lưu và chốt điểm chính thức cho sinh viên.');
      onUpdated?.();
    } catch (e) {
      setErr(e.message);
    } finally {
      setSaveBusy(false);
    }
  };

  const hasTranscript = (candidate.questions || []).some(q => q.transcript || (q.followUps && q.followUps.length > 0));

  return (
    <details className="vr-cand-card" open={defaultOpen}>
      <summary className="vr-cand-summary">
        <div className="vr-cand-summary__left">
          <strong>{candidate.studentName}</strong>
          <span className="wf-muted">{candidate.studentEmail} · {vnTime(candidate.scheduledTime)}</span>
        </div>
        <div className="vr-cand-summary__right">
          <Status value={candidate.status} />
          <span className="vr-badge vr-badge--ai">
            AI đề xuất: {res?.aiScore != null ? `${Number(res.aiScore).toFixed(1)}/10` : '—'}
          </span>
          <span className={`vr-badge ${res?.isFinalized ? 'vr-badge--final' : 'vr-badge--pending'}`}>
            {res?.isFinalized && res?.finalScore != null
              ? `✓ Điểm cuối: ${Number(res.finalScore).toFixed(1)}/10`
              : 'Chờ GV duyệt'}
          </span>
        </div>
      </summary>

      <div className="vr-cand-body">
        <ol className="vr-qlist">
          {(candidate.questions || []).map((q, idx) => (
            <li key={q.examQuestionId} className="vr-qcard">
              <div className="vr-qcard__head">
                <div>
                  <span className="vr-qcard__num">Câu {idx + 1}</span>
                  <strong className="vr-qcard__title">{q.questionText}</strong>
                </div>
                <div className="vr-qcard__badges">
                  <span className="vr-tag">{diffLabels[q.difficulty] || q.difficulty}</span>
                  {q.evaluation && (
                    <span className="vr-qscore">
                      AI: {Number(q.evaluation.aiScore).toFixed(1)} / 10
                      {q.evaluation.confidence != null && ` (${Math.round(Number(q.evaluation.confidence) * 100)}%)`}
                    </span>
                  )}
                </div>
              </div>

              {(q.durationSeconds != null || q.wordCount > 0) && (
                <div className="vr-signals">
                  <span>⏱ Thời gian trả lời: {q.durationSeconds ?? 0} giây</span>
                  <span>📝 Độ dài: {q.wordCount ?? 0} từ</span>
                  <span>
                    🗣 Độ trôi chảy: <strong>{q.fluencyLabel || 'Chưa xác định'}</strong>
                    {q.wordsPerMinute ? ` (~${q.wordsPerMinute} từ/phút)` : ''}
                  </span>
                </div>
              )}

              <div className="vr-compare-grid">
                <div className="vr-compare-box">
                  <span className="vr-compare-box__label">Transcript trả lời của Sinh viên</span>
                  <p className="vr-compare-box__text">
                    {q.transcript ? q.transcript : <em className="wf-muted">Chưa có câu trả lời được ghi nhận.</em>}
                  </p>
                  {q.followUps?.length > 0 && (
                    <div className="vr-followups">
                      {q.followUps.map((f, fi) => (
                        <div key={f.followUpId || fi} className="vr-followup-item">
                          <strong>↳ Câu hỏi đào sâu {fi + 1}: {f.questionText}</strong>
                          {f.reason && <small className="wf-muted">Lý do hỏi thêm: {f.reason}</small>}
                          <p>{f.answerTranscript || <em className="wf-muted">Không có câu trả lời.</em>}</p>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                <div className="vr-compare-box vr-compare-box--expected">
                  <span className="vr-compare-box__label">Đáp án mẫu / Tiêu chí đối chiếu</span>
                  <p className="vr-compare-box__text">
                    {q.expectedAnswer || <em className="wf-muted">Chưa cập nhật đáp án mẫu.</em>}
                  </p>
                </div>
              </div>

              {q.evaluation && (
                <div className="vr-eval-box">
                  <div className="vr-eval-box__feedback">
                    <strong>Nhận xét của AI:</strong> {q.evaluation.feedback}
                  </div>
                  <div className="vr-eval-grid">
                    <div className="vr-eval-pill is-good">
                      <span>✓ Điểm mạnh</span>
                      <p>{q.evaluation.strengths || '—'}</p>
                    </div>
                    <div className="vr-eval-pill is-warn">
                      <span>⚠ Điểm yếu</span>
                      <p>{q.evaluation.weaknesses || '—'}</p>
                    </div>
                    <div className="vr-eval-pill is-miss">
                      <span>🔍 Ý còn thiếu</span>
                      <p>{q.evaluation.missingPoints || '—'}</p>
                    </div>
                  </div>
                </div>
              )}
            </li>
          ))}
        </ol>

        {examId && (
          <form className="vr-grade-bar" onSubmit={handleSaveGrade}>
            <div className="vr-grade-bar__top">
              <div>
                <span className="wf-eyebrow">Human-in-the-loop · Giảng viên quyết định điểm cuối</span>
                <h3>Chấm điểm & Nhận xét của Giảng viên</h3>
                <p className="wf-muted" style={{ margin: 0 }}>
                  AI chỉ hỗ trợ đối chiếu transcript với đáp án để đề xuất điểm. Giảng viên điều chỉnh và chốt điểm chính thức.
                </p>
              </div>
              <button
                type="button"
                className="wf-button wf-secondary"
                disabled={aiBusy || saveBusy}
                onClick={handleAiEvaluate}
              >
                {aiBusy ? 'AI đang chấm…' : res?.aiScore != null ? 'Chấm lại bằng AI' : 'Chấm gợi ý bằng AI'}
              </button>
            </div>

            {err && <p className="wf-error" role="alert" style={{ margin: '12px 0 0' }}>{err}</p>}
            {msg && <p className="wf-notice" role="status" style={{ margin: '12px 0 0' }}>{msg}</p>}

            <div className="vr-grade-inputs">
              <div className="vr-grade-score-col">
                <label>
                  Điểm quyết định (0 – 10)
                  <input
                    type="number"
                    step="0.1"
                    min="0"
                    max="10"
                    required
                    value={score}
                    onChange={e => setScore(e.target.value)}
                    placeholder="VD: 8.5"
                  />
                </label>
                {res?.aiScore != null && (
                  <button
                    type="button"
                    className="vr-use-ai-btn"
                    onClick={() => setScore(String(res.aiScore))}
                  >
                    Dùng điểm AI đề xuất ({Number(res.aiScore).toFixed(1)})
                  </button>
                )}
              </div>

              <div className="vr-grade-comment-col">
                <label>
                  Nhận xét của Giảng viên
                  <textarea
                    rows={2}
                    maxLength={2000}
                    value={comment}
                    onChange={e => setComment(e.target.value)}
                    placeholder={hasTranscript ? 'Nhập nhận xét tổng kết cho sinh viên…' : 'Sinh viên chưa hoàn thành câu trả lời hoặc vắng thi…'}
                  />
                </label>
              </div>
            </div>

            <div className="vr-grade-actions">
              {res?.isFinalized && (
                <span className="wf-muted">
                  Đã chốt điểm bởi <strong>{res.finalizedByName || 'Giảng viên'}</strong> lúc {vnTime(res.finalizedAt)}
                </span>
              )}
              <button type="submit" className="wf-button" disabled={saveBusy || aiBusy}>
                {saveBusy ? 'Đang lưu…' : res?.isFinalized ? 'Cập nhật điểm đã chốt' : '✓ Xác nhận & Chốt điểm cuối cùng'}
              </button>
            </div>
          </form>
        )}
      </div>
    </details>
  );
}

// Uses dev's per-candidate presentation with actual server assignments and AI-assisted grading.
export default function CandidateQuestionDistribution({ examId, candidates = [], mainCount = 0, onUpdated }) {
  const [batchBusy, setBatchBusy] = useState(false);
  const [batchNotice, setBatchNotice] = useState('');

  const questions = candidates.flatMap(candidate => candidate.questions || []);
  const distinct = new Set(questions.map(question => question.questionId)).size;
  const overlap = candidates.slice(1).reduce((total, candidate, index) => {
    const previous = new Set((candidates[index].questions || []).map(question => question.questionId));
    return total + (candidate.questions || []).filter(question => previous.has(question.questionId)).length;
  }, 0);

  const completedCount = candidates.filter(c => c.status === 'Completed').length;
  const finalizedCount = candidates.filter(c => c.result?.isFinalized).length;

  const handleBatchAi = async () => {
    if (!examId) return;
    setBatchBusy(true);
    setBatchNotice('');
    try {
      const targets = candidates.filter(c => c.status === 'Completed' || (c.questions || []).some(q => q.transcript));
      const list = targets.length > 0 ? targets : candidates;
      for (const c of list) {
        await api.post(`/api/Exams/${examId}/candidates/${c.candidateId}/evaluate`);
      }
      setBatchNotice(`Đã hoàn tất chấm gợi ý AI cho ${list.length} thí sinh.`);
      onUpdated?.();
    } catch (e) {
      setBatchNotice(`Lỗi khi chấm AI: ${e.message}`);
    } finally {
      setBatchBusy(false);
    }
  };

  return (
    <section className="wf-panel" style={{ marginTop: 20 }} aria-labelledby="distribution-title">
      <div className="wf-row" style={{ flexWrap: 'wrap' }}>
        <div>
          <span className="wf-eyebrow">Phân câu hỏi & Hỗ trợ chấm điểm bằng AI (Human-in-the-loop)</span>
          <h2 id="distribution-title" style={{ margin: '4px 0 6px' }}>Câu hỏi đã phân cho sinh viên & Bảng chấm điểm</h2>
          <p className="wf-muted" style={{ margin: 0 }}>
            {mainCount} câu chính / sinh viên · {distinct} câu khác nhau · {overlap} lượt trùng liền kề ·{' '}
            <strong>{completedCount}/{candidates.length}</strong> đã hoàn thành ·{' '}
            <strong>{finalizedCount}/{candidates.length}</strong> đã chốt điểm cuối
          </p>
        </div>
        {examId && candidates.length > 0 && (
          <button type="button" className="wf-button wf-secondary" disabled={batchBusy} onClick={handleBatchAi}>
            {batchBusy ? 'AI đang chấm hàng loạt…' : 'Chấm gợi ý AI toàn bộ'}
          </button>
        )}
      </div>

      {batchNotice && <p className="wf-notice" role="status" style={{ marginTop: 14 }}>{batchNotice}</p>}

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
