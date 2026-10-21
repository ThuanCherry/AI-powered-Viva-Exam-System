import { vnTime } from '../lib/time.js';
import '../styles/CandidateQuestionDistribution.css';

// Uses dev's per-candidate presentation with actual server assignments only.
export default function CandidateQuestionDistribution({ candidates = [], mainCount = 0 }) {
  const questions = candidates.flatMap(candidate => candidate.questions || []);
  const distinct = new Set(questions.map(question => question.questionId)).size;
  const overlap = candidates.slice(1).reduce((total, candidate, index) => {
    const previous = new Set((candidates[index].questions || []).map(question => question.questionId));
    return total + (candidate.questions || []).filter(question => previous.has(question.questionId)).length;
  }, 0);
  return <section className="wf-panel" style={{ marginTop: 20 }} aria-labelledby="distribution-title">
    <h2 id="distribution-title">Câu hỏi đã phân cho sinh viên</h2>
    <p className="wf-muted">Dữ liệu đã lưu · {mainCount} câu chính / sinh viên · {distinct} câu khác nhau · {overlap} lượt trùng giữa hai sinh viên liền kề</p>
    {questions.length === 0 ? <p>Chưa có câu hỏi được phân.</p> : candidates.map(candidate => <details key={candidate.studentId} className="wf-history">
      <summary><strong>{candidate.studentName}</strong> · {vnTime(candidate.scheduledTime)} · {(candidate.questions || []).length} câu</summary>
      <ol>{(candidate.questions || []).map(question => <li key={question.examQuestionId}>
        <p>{question.questionText}</p><small className="wf-muted">{({ Easy: 'Dễ', Medium: 'Trung bình', Hard: 'Khó' })[question.difficulty] || question.difficulty}</small>
      </li>)}</ol>
    </details>)}
  </section>;
}
