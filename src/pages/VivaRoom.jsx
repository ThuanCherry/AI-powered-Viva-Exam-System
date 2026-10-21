import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { HubConnectionBuilder } from '@microsoft/signalr';
import AppShell from '../components/Sidebar.jsx';
import { api, BASE_URL } from '../api/client.js';
import { getSession } from '../auth/session.js';
import { Status } from './Exams.jsx';
import { vnTime } from '../lib/time.js';
import { startLiveMicrophone } from '../audio/liveMicrophone.js';
import '../styles/Workflow.css';
import { SCHEDULE_CHANGED } from '../hooks/useScheduleRealtime.js';

function remaining(deadline, now) {
  const secs = Math.max(0, Math.ceil((new Date(deadline).getTime() - now) / 1000));
  return `${Math.floor(secs / 60)}:${String(secs % 60).padStart(2, '0')}`;
}

export default function VivaRoom() {
  const { examId } = useParams();
  const [state, setState] = useState(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [transcript, setTranscript] = useState('');
  const [connection, setConnection] = useState('Đang kết nối');
  const [recording, setRecording] = useState(false);
  const [pending, setPending] = useState(0);
  const [micStarting, setMicStarting] = useState(false);
  const [interim, setInterim] = useState('');
  const [speechBusy, setSpeechBusy] = useState(false);
  const [now, setNow] = useState(() => Date.now());
  const current = useRef(null);
  const mounted = useRef(true);
  const manualDirty = useRef(false);
  const offset = useRef(0);
  const capture = useRef(null);
  const micAbort = useRef(null);
  const hubConnection = useRef(null);
  const speechAbort = useRef(null);
  const speechCache = useRef(null);
  const audioUrl = useRef(null);
  const audio = useRef(null);
  const apply = useCallback(next => {
    offset.current = new Date(next.serverTime).getTime() - Date.now();
    if (next.turn?.turnId !== current.current) { current.current = next.turn?.turnId; manualDirty.current = false; setTranscript(next.turn?.transcript || ''); setInterim(''); }
    else if (!manualDirty.current && !capture.current) setTranscript(next.turn?.transcript || '');
    setState(next);
  }, []);
  const refresh = useCallback(async () => apply(await api.get(`/api/Exams/${examId}/attempt`)), [examId, apply]);
  const stop = useCallback(() => {
    capture.current?.stop();
  }, []);
  const cancelMic = useCallback(() => { micAbort.current?.abort(); capture.current?.cancel(); capture.current = null; }, []);

  useEffect(() => {
    let alive = true;
    mounted.current = true;
    const load = () => refresh().catch(e => { if (alive) setError(e.message); });
    load(); const poll = setInterval(load, 5000); const tick = setInterval(() => setNow(Date.now() + offset.current), 1000);
    const hub = new HubConnectionBuilder().withUrl(`${BASE_URL}/hubs/exam`, { accessTokenFactory: () => getSession()?.token || '' }).withAutomaticReconnect().build();
    hubConnection.current = hub;
    const join = async () => { await hub.invoke('JoinExam', Number(examId)); if (alive) setConnection('Đã kết nối SignalR'); };
    hub.on('AttemptUpdated', load); hub.on('TranscriptUpdated', load); hub.on('ScheduleChanged', load);
    const scheduleChanged = event => { if (!event.detail?.examId || event.detail.examId === Number(examId)) load(); };
    window.addEventListener(SCHEDULE_CHANGED, scheduleChanged);
    hub.onreconnecting(() => { cancelMic(); if (alive) { setConnection('Đang nối lại · transcript đã xác nhận được giữ'); setError('Mạng gián đoạn. Microphone đã dừng; sau khi nối lại hãy thu tiếp phần còn thiếu.'); } });
    hub.onreconnected(() => { load(); join().catch(() => {}); });
    hub.onclose(() => { if (alive) setConnection('REST dự phòng · đồng bộ mỗi 5 giây'); });
    hub.start().then(join).catch(() => { if (alive) setConnection('REST dự phòng · đồng bộ mỗi 5 giây'); });
    return () => { alive = false; mounted.current = false; window.removeEventListener(SCHEDULE_CHANGED, scheduleChanged); clearInterval(poll); clearInterval(tick); cancelMic(); speechAbort.current?.abort(); hub.stop(); audio.current?.pause(); if (audioUrl.current) URL.revokeObjectURL(audioUrl.current); };
  }, [examId, refresh, cancelMic]);

  const speak = useCallback(async (turnId, signal) => {
    speechAbort.current?.abort();
    const controller = new AbortController(); speechAbort.current = controller;
    const abort = () => controller.abort(); signal?.addEventListener('abort', abort, { once: true });
    const timeout = setTimeout(abort, 30000);
    setSpeechBusy(true);
    try {
      audio.current?.pause();
      if (audioUrl.current) URL.revokeObjectURL(audioUrl.current);
      const blob = speechCache.current?.turnId === turnId ? speechCache.current.blob : await api.get(`/api/Exams/${examId}/attempt/tts?turnId=${encodeURIComponent(turnId)}`, { binary: true, signal: controller.signal });
      if (current.current !== turnId || signal?.aborted || controller.signal.aborted) return;
      speechCache.current = { turnId, blob };
      const url = URL.createObjectURL(blob); const player = new Audio(url); audio.current = player;
      audioUrl.current = url;
      player.onended = () => URL.revokeObjectURL(url);
      try { await player.play(); } catch { URL.revokeObjectURL(url); throw new Error('Trình duyệt chặn tự phát âm thanh. Nhấn Nghe câu hỏi để phát.'); }
    } catch (e) { if (e.name !== 'AbortError') setError(e.message); else if (!signal?.aborted) setError('TTS đã hủy hoặc quá 30 giây. Có thể bấm Nghe câu hỏi để thử lại.'); }
    finally { clearTimeout(timeout); signal?.removeEventListener('abort', abort); if (speechAbort.current === controller && mounted.current) setSpeechBusy(false); }
  }, [examId]);
  const turnId = state?.turn?.turnId;
  const ttsConfigured = state?.providers?.tts === 'Configured';
  useEffect(() => {
    const cleanupMic = setTimeout(cancelMic, 0);
    // Refreshing/reconnecting or enabling configuration must not spend TTS quota automatically.
    return () => { clearTimeout(cleanupMic); speechAbort.current?.abort(); audio.current?.pause(); };
  }, [turnId, cancelMic]);

  const run = async fn => { setBusy(true); setError(''); try { apply(await fn()); } catch (e) { setError(e.message); } finally { setBusy(false); } };
  const startMic = async () => {
    setError(''); setMicStarting(true);
    try {
      if (manualDirty.current && transcript.trim()) throw new Error('Bạn đã sửa transcript bằng tay. Nộp lượt này hoặc xóa phần nhập tay trước khi thu âm lại để tránh mất nội dung.');
      const captureTurn = current.current;
      audio.current?.pause(); setInterim('');
      const controller = new AbortController(); micAbort.current = controller;
      let unconfirmed = '';
      const session = await startLiveMicrophone({ hub: hubConnection.current, examId, turnId: captureTurn, signal: controller.signal,
        onEvent(event) {
          if (current.current !== captureTurn || !mounted.current) return;
          if (event.type === 'interim') { unconfirmed = event.text; setInterim(event.text); }
          if (event.type === 'final' || event.type === 'ready') { if (event.transcript != null) setTranscript(event.transcript); unconfirmed = ''; setInterim(''); }
        },
        onState(value) {
          if (!mounted.current) return;
          setRecording(value === 'recording'); setMicStarting(value === 'connecting'); setPending(value === 'draining' ? 1 : 0);
          if (value === 'idle') { capture.current = null; if (unconfirmed) setError('Đoạn chữ tạm cuối chưa được Google xác nhận. Kiểm tra phần Chưa xác nhận, thu lại hoặc bổ sung bằng tay trước khi nộp.'); }
        },
        onError(message) { if (mounted.current) setError(message); },
      });
      if (controller.signal.aborted || current.current !== captureTurn) session.cancel(); else capture.current = session;
    } catch (e) { cancelMic(); setMicStarting(false); setError(e.name === 'NotAllowedError' ? 'Quyền microphone bị từ chối. Cho phép mic trong cài đặt trình duyệt hoặc nhập văn bản dự phòng.' : e.message); }
  };
  return <AppShell title="Phòng vấn đáp" subtitle="Câu hỏi và câu trả lời được lưu theo phiên thi"><div className="wf-page wf-room">
    <div className="wf-row"><Link className="wf-back" to="/exams">← Lịch thi</Link><span className="wf-connection" role="status">{connection}</span></div>
    {error && <p className="wf-error" role="alert">{error}</p>}
    {!state ? <p>Đang tải phiên thi…</p> : <>
      <section className="wf-panel" style={{ marginTop: 20 }}>
        <div className="wf-row"><Status value={state.status} />{state.deadline && state.status === 'InProgress' && <span className="wf-clock">Phiên thi còn {remaining(state.deadline, now)}</span>}</div>
        {state.status === 'Waiting' && <div className="wf-empty"><h2>Phòng chờ sẵn sàng</h2><p>Slot của bạn: {vnTime(state.scheduledTime)}</p><p>Server kiểm tra quyền và giờ bắt đầu. Chuẩn bị microphone trước khi vào thi.</p><button className="wf-button" disabled={busy} onClick={() => run(() => api.post(`/api/Exams/${examId}/attempt/start`))}>{busy ? 'Đang kiểm tra…' : 'Bắt đầu thi'}</button></div>}
        {state.status === 'InProgress' && state.turn && <>
          <p className="wf-muted">Câu chính {state.turn.mainOrder}/{state.mainQuestionCount} {state.turn.isFollowUp && '· Câu hỏi đào sâu'} · Follow-up {state.followUpsUsed}/{state.maxFollowUpCount}</p>
          <progress value={state.completedMain} max={state.mainQuestionCount} />
          <h2 className="wf-question">{state.turn.text}</h2>
          <p className="wf-clock">Lượt trả lời còn {remaining(state.turnDeadline, now)}</p>
          <div className="wf-tools"><button className="wf-button wf-secondary" disabled={!ttsConfigured || busy || recording || pending > 0 || micStarting || speechBusy} onClick={() => speak(state.turn.turnId)}>{speechBusy ? 'Đang tạo giọng đọc…' : 'Nghe câu hỏi'}</button><button className="wf-button wf-secondary" disabled={busy || micStarting || speechBusy || state.providers.stt !== 'Configured' || (!recording && pending > 0)} onClick={recording ? stop : startMic}>{micStarting ? 'Đang mở Gemini Live…' : recording ? '■ Dừng & chốt transcript' : '● Trả lời bằng mic'}</button>{pending > 0 && <span role="status">Đang chốt transcript cuối…</span>}{(micStarting || pending > 0) && <button className="wf-button wf-secondary" onClick={cancelMic}>Hủy thu âm</button>}</div>
          <p className="wf-muted">Gemini Live Transcribe · tiếng Việt · audio PCM gửi liên tục mỗi 100ms. Chữ tạm cập nhật khi nói; chỉ transcript đã xác nhận được lưu vào phiên thi.</p>
          {interim && <p className="wf-fallback" role="status"><strong>{recording ? 'Đang nhận: ' : 'Chưa xác nhận: '}</strong>{interim}</p>}
          <label htmlFor="transcript">Transcript / nhập văn bản dự phòng</label>
          <textarea id="transcript" className="wf-transcript" maxLength={12000} value={transcript} disabled={busy || recording || micStarting || pending > 0} onChange={e => { manualDirty.current = true; setTranscript(e.target.value); }} placeholder="Transcript từ microphone sẽ hiện tại đây. Có thể sửa hoặc nhập tay khi cần." />
          <div className="wf-tools"><button className="wf-button" disabled={busy || recording || micStarting || pending > 0} onClick={() => run(() => api.post(`/api/Exams/${examId}/attempt/answer`, { turnId: state.turn.turnId, transcript }))}>{busy ? 'Đang lưu & xử lý…' : 'Nộp câu trả lời →'}</button><button className="wf-button wf-secondary" disabled={busy || recording || micStarting || pending > 0} onClick={() => run(() => api.post(`/api/Exams/${examId}/attempt/answer`, { turnId: state.turn.turnId, transcript: '' }))}>Bỏ qua lượt</button></div>
        </>}
        {state.status === 'Completed' && <div className="wf-empty"><h2>Đã kết thúc phiên thi</h2><p>Các câu trả lời đã được lưu. Bạn có thể xem lại bên dưới.</p></div>}
        {!['Waiting', 'Completed', 'InProgress'].includes(state.status) && <p>Phiên thi không khả dụng: {state.status}.</p>}
      </section>
      {(state.providers.llm !== 'Configured' || state.providers.stt !== 'Configured' || state.providers.tts !== 'Configured') && <p className="wf-fallback">LLM: {state.providers.llm} · STT: {state.providers.stt} · TTS: {state.providers.tts}. {state.providers.missing?.length > 0 && <>Backend chưa nạp: {state.providers.missing.join(', ')}. </>}Môi trường: {state.providers.configurationEnvironment || 'chưa xác định'}. Có thể nhập văn bản dự phòng.</p>}
      {state.providerWarning && <p className="wf-fallback">Lỗi provider được lưu gần nhất: {state.providerWarning}</p>}
      {state.history?.length > 0 && <section className="wf-panel" style={{ marginTop: 20 }}><h2>Lịch sử phiên thi</h2>{state.history.map((h, i) => <article className="wf-history" key={i}><strong>{i + 1}. {h.question}</strong><p>{h.transcript || (h.completed ? 'Không có câu trả lời.' : 'Chưa nộp.')}</p>{h.followUps?.map((f, j) => <div key={j}><strong>↳ {f.question}</strong><p>{f.transcript || (f.answered ? 'Không có câu trả lời.' : 'Đang chờ trả lời.')}</p></div>)}</article>)}</section>}
    </>}
  </div></AppShell>;
}
