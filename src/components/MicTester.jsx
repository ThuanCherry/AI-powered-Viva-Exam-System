import { useCallback, useEffect, useRef, useState } from 'react';

export default function MicTester({ selectedDeviceId, onSelectDevice, examMicActive, defaultExpanded = false }) {
  const [expanded, setExpanded] = useState(defaultExpanded);
  const [devices, setDevices] = useState([]);
  const [testing, setTesting] = useState(false);
  const [level, setLevel] = useState(0);
  const [voiceDetected, setVoiceDetected] = useState(false);
  const [sampleUrl, setSampleUrl] = useState('');
  const [playing, setPlaying] = useState(false);
  const [err, setErr] = useState('');

  const streamRef = useRef(null);
  const ctxRef = useRef(null);
  const rafRef = useRef(null);
  const recorderRef = useRef(null);
  const chunksRef = useRef([]);
  const playerRef = useRef(null);

  const loadDevices = useCallback(async () => {
    if (!navigator.mediaDevices?.enumerateDevices) return;
    try {
      const list = await navigator.mediaDevices.enumerateDevices();
      const mics = list.filter(d => d.kind === 'audioinput');
      setDevices(mics);
      if (!selectedDeviceId && mics.length > 0 && mics[0].deviceId) {
        onSelectDevice?.(mics[0].deviceId);
      }
    } catch {
      // Ignore enumerate error before permission
    }
  }, [selectedDeviceId, onSelectDevice]);

  const stopTest = useCallback(() => {
    if (rafRef.current) cancelAnimationFrame(rafRef.current);
    rafRef.current = null;
    if (recorderRef.current && recorderRef.current.state !== 'inactive') {
      try { recorderRef.current.stop(); } catch { /* ignore */ }
    }
    recorderRef.current = null;
    if (streamRef.current) {
      streamRef.current.getTracks().forEach(t => t.stop());
      streamRef.current = null;
    }
    if (ctxRef.current) {
      ctxRef.current.close().catch(() => {});
      ctxRef.current = null;
    }
    setTesting(false);
    setLevel(0);
  }, []);

  useEffect(() => {
    loadDevices();
    return () => {
      stopTest();
      if (playerRef.current) playerRef.current.pause();
    };
  }, [loadDevices, stopTest]);

  // Tự động ngắt Test Mic ngay khi sinh viên bấm thu âm thật trong bài thi
  useEffect(() => {
    if (examMicActive && testing) {
      stopTest();
    }
  }, [examMicActive, testing, stopTest]);

  const startTest = async () => {
    setErr('');
    setVoiceDetected(false);
    if (sampleUrl) {
      URL.revokeObjectURL(sampleUrl);
      setSampleUrl('');
    }
    try {
      if (!navigator.mediaDevices?.getUserMedia) {
        throw new Error('Trình duyệt không hỗ trợ truy cập Microphone.');
      }
      const constraints = {
        audio: selectedDeviceId
          ? { deviceId: { ideal: selectedDeviceId }, echoCancellation: true, noiseSuppression: true }
          : { echoCancellation: true, noiseSuppression: true }
      };
      const stream = await navigator.mediaDevices.getUserMedia(constraints);
      streamRef.current = stream;
      await loadDevices();

      const AudioCtx = window.AudioContext || window.webkitAudioContext;
      const ctx = new AudioCtx();
      ctxRef.current = ctx;
      await ctx.resume();

      const source = ctx.createMediaStreamSource(stream);
      const analyser = ctx.createAnalyser();
      analyser.fftSize = 256;
      analyser.smoothingTimeConstant = 0.75;
      source.connect(analyser);

      // Ghi âm cục bộ trên trình duyệt để sinh viên nghe thử lại giọng mình
      chunksRef.current = [];
      if (window.MediaRecorder) {
        try {
          const recorder = new MediaRecorder(stream);
          recorderRef.current = recorder;
          recorder.ondataavailable = e => {
            if (e.data && e.data.size > 0) chunksRef.current.push(e.data);
          };
          recorder.onstop = () => {
            if (chunksRef.current.length > 0) {
              const blob = new Blob(chunksRef.current, { type: recorder.mimeType || 'audio/webm' });
              setSampleUrl(URL.createObjectURL(blob));
            }
          };
          recorder.start();
        } catch {
          // MediaRecorder optional fallback
        }
      }

      const data = new Uint8Array(analyser.frequencyBinCount);
      const tick = () => {
        if (!ctxRef.current) return;
        analyser.getByteFrequencyData(data);
        let sum = 0;
        for (let i = 0; i < data.length; i++) sum += data[i];
        const avg = sum / data.length;
        const pct = Math.min(100, Math.round((avg / 90) * 100));
        setLevel(pct);
        if (pct >= 12) setVoiceDetected(true);
        rafRef.current = requestAnimationFrame(tick);
      };
      setTesting(true);
      rafRef.current = requestAnimationFrame(tick);
    } catch (e) {
      stopTest();
      setErr(
        e.name === 'NotAllowedError'
          ? 'Trình duyệt đang chặn quyền Microphone. Hãy bấm vào biểu tượng ổ khóa trên thanh địa chỉ để cho phép.'
          : e.message
      );
    }
  };

  const playSample = () => {
    if (!sampleUrl) return;
    if (playerRef.current) {
      playerRef.current.pause();
    }
    const audio = new Audio(sampleUrl);
    playerRef.current = audio;
    setPlaying(true);
    audio.onended = () => setPlaying(false);
    audio.onerror = () => setPlaying(false);
    audio.play().catch(() => setPlaying(false));
  };

  return (
    <div className="vr-mictest">
      <div className="vr-mictest__header">
        <div className="vr-mictest__title">
          <span className={`vr-mictest__dot ${voiceDetected ? 'is-ok' : testing ? 'is-active' : ''}`} />
          <strong>Kiểm tra Microphone (Chạy cục bộ · Không gọi AI)</strong>
          {voiceDetected && <span className="vr-mictest__ok-tag">✓ Mic hoạt động tốt</span>}
        </div>
        <button
          type="button"
          className="vr-mictest__toggle"
          onClick={() => {
            if (expanded && testing) stopTest();
            setExpanded(v => !v);
          }}
        >
          {expanded ? 'Thu gọn ▲' : 'Mở kiểm tra Mic ▼'}
        </button>
      </div>

      {expanded && (
        <div className="vr-mictest__body">
          {err && <p className="wf-error" style={{ margin: '0 0 12px' }}>{err}</p>}

          <div className="vr-mictest__controls">
            <label className="vr-mictest__device">
              <span>Thiết bị thu âm</span>
              <select
                value={selectedDeviceId || ''}
                disabled={testing || examMicActive}
                onChange={e => onSelectDevice?.(e.target.value)}
              >
                {devices.length === 0 && <option value="">Microphone mặc định của hệ thống</option>}
                {devices.map((d, i) => (
                  <option key={d.deviceId || i} value={d.deviceId}>
                    {d.label || `Microphone ${i + 1}`}
                  </option>
                ))}
              </select>
            </label>

            <div className="vr-mictest__btns">
              <button
                type="button"
                className={`wf-button ${testing ? '' : 'wf-secondary'}`}
                disabled={examMicActive}
                onClick={testing ? stopTest : startTest}
              >
                {testing ? '■ Dừng kiểm tra' : '🎙 Bắt đầu thử Mic'}
              </button>

              {sampleUrl && !testing && (
                <button
                  type="button"
                  className="wf-button wf-secondary"
                  disabled={playing}
                  onClick={playSample}
                >
                  {playing ? '🔊 Đang phát lại…' : '▶ Nghe lại giọng vừa thu'}
                </button>
              )}
            </div>
          </div>

          <div className="vr-mictest__meter-wrap">
            <div className="vr-mictest__bars" aria-label="Cường độ âm thanh microphone">
              {Array.from({ length: 16 }).map((_, idx) => {
                const threshold = ((idx + 1) / 16) * 100;
                const active = testing && level >= threshold * 0.75;
                return (
                  <span
                    key={idx}
                    className={`vr-mictest__bar ${active ? (idx > 11 ? 'is-high' : 'is-on') : ''}`}
                  />
                );
              })}
            </div>
            <span className="vr-mictest__status">
              {!testing
                ? voiceDetected
                  ? '✓ Đã xác nhận microphone thu âm tốt. Bạn có thể bấm "Nghe lại giọng vừa thu" hoặc bắt đầu làm bài.'
                  : 'Bấm "🎙 Bắt đầu thử Mic" và nói thử một câu để kiểm tra sóng âm.'
                : voiceDetected
                ? `✓ Tín hiệu tốt (${level}%) — Microphone đang thu âm rõ ràng! Bấm "Dừng kiểm tra" để nghe lại.`
                : 'Đang lắng nghe… Hãy nói thử "Alo 1, 2, 3" vào microphone.'}
            </span>
          </div>
        </div>
      )}
    </div>
  );
}
