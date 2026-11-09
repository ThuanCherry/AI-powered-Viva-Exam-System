import { useEffect, useRef, useState } from 'react';
import { getSession } from '../auth/session.js';

function getStorageKeys(examId) {
  const session = getSession();
  const userId = session?.user?.userId || session?.user?.id || 'anon';
  const email = (session?.user?.email || '').toLowerCase();
  return {
    userKey: `aives_anticheat_${examId}_u_${userId}`,
    emailKey: email ? `aives_anticheat_${examId}_e_${email}` : null
  };
}

export function readCandidateAntiCheatEvents(examId, studentEmail) {
  if (!examId || !studentEmail) return [];
  try {
    const raw = localStorage.getItem(`aives_anticheat_${examId}_e_${studentEmail.toLowerCase()}`);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

function playWarningTone() {
  try {
    const AudioCtx = window.AudioContext || window.webkitAudioContext;
    if (!AudioCtx) return;
    const ctx = new AudioCtx();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = 'triangle';
    osc.frequency.setValueAtTime(587.33, ctx.currentTime); // D5
    osc.frequency.setValueAtTime(440, ctx.currentTime + 0.12); // A4
    gain.gain.setValueAtTime(0.08, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.28);
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.start();
    osc.stop(ctx.currentTime + 0.28);
    setTimeout(() => ctx.close().catch(() => {}), 400);
  } catch {
    // Ignore audio context restriction
  }
}

export default function AntiCheatMonitor({ examId, active }) {
  const { userKey, emailKey } = getStorageKeys(examId);
  const [events, setEvents] = useState(() => {
    try {
      const saved = localStorage.getItem(userKey);
      return saved ? JSON.parse(saved) : [];
    } catch {
      return [];
    }
  });
  const [showLog, setShowLog] = useState(false);
  const [justReturned, setJustReturned] = useState(null);
  const lastTriggerRef = useRef(0);
  const awayStartRef = useRef(null);

  const saveEvents = next => {
    setEvents(next);
    try {
      const json = JSON.stringify(next);
      localStorage.setItem(userKey, json);
      if (emailKey) localStorage.setItem(emailKey, json);
    } catch {
      // Ignore storage quota error
    }
  };

  useEffect(() => {
    if (!active) return;

    const recordLeave = reason => {
      const now = Date.now();
      if (now - lastTriggerRef.current < 1500) return;
      lastTriggerRef.current = now;
      awayStartRef.current = now;

      const timeStr = new Date(now).toLocaleTimeString('vi-VN', {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
      });

      setEvents(prev => {
        const next = [
          {
            id: now,
            time: timeStr,
            reason,
            awaySeconds: null
          },
          ...prev
        ];
        try {
          const json = JSON.stringify(next);
          localStorage.setItem(userKey, json);
          if (emailKey) localStorage.setItem(emailKey, json);
        } catch {
          // Ignore storage error
        }
        return next;
      });
      playWarningTone();
    };

    const recordReturn = () => {
      if (!awayStartRef.current) return;
      const awaySecs = Math.max(1, Math.round((Date.now() - awayStartRef.current) / 1000));
      awayStartRef.current = null;

      setEvents(prev => {
        if (prev.length === 0) return prev;
        const [latest, ...rest] = prev;
        if (latest.awaySeconds != null) return prev;
        const updated = [{ ...latest, awaySeconds: awaySecs }, ...rest];
        try {
          const json = JSON.stringify(updated);
          localStorage.setItem(userKey, json);
          if (emailKey) localStorage.setItem(emailKey, json);
        } catch {
          // Ignore storage error
        }
        setJustReturned({ count: updated.length, awaySecs, reason: latest.reason });
        return updated;
      });
    };

    const handleVisibilityChange = () => {
      if (document.hidden) {
        recordLeave('Chuyển sang Tab khác hoặc ẩn trình duyệt');
      } else {
        recordReturn();
      }
    };

    const handleWindowBlur = () => {
      if (document.hidden) return;
      recordLeave('Rời khỏi cửa sổ phòng thi (Mất tiêu điểm màn hình)');
    };

    const handleWindowFocus = () => {
      recordReturn();
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);
    window.addEventListener('blur', handleWindowBlur);
    window.addEventListener('focus', handleWindowFocus);

    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      window.removeEventListener('blur', handleWindowBlur);
      window.removeEventListener('focus', handleWindowFocus);
    };
  }, [active, userKey, emailKey]);

  const count = events.length;
  const hasViolations = count > 0;

  return (
    <div className={`vr-anticheat ${hasViolations ? 'is-warn' : 'is-safe'}`} role="region" aria-label="Giám sát phòng thi">
      <div className="vr-anticheat__bar">
        <div className="vr-anticheat__status">
          <span className={`vr-anticheat__dot ${hasViolations ? 'is-warn' : 'is-safe'}`} />
          {hasViolations ? (
            <span>
              <strong>⚠️ Cảnh báo rời màn hình thi: {count} lần</strong>
              <span className="vr-anticheat__sub">
                — Hệ thống đã ghi nhận hành vi chuyển tab / rời cửa sổ trong lúc đang thi.
              </span>
            </span>
          ) : (
            <span>
              <strong>🛡️ Giám sát phòng thi (Anti-Cheat): Đang bật</strong>
              <span className="vr-anticheat__sub">
                — Vui lòng không chuyển tab hoặc mở cửa sổ khác trong suốt quá trình thi (0 lần vi phạm).
              </span>
            </span>
          )}
        </div>

        {hasViolations && (
          <button
            type="button"
            className="vr-anticheat__btn"
            onClick={() => setShowLog(v => !v)}
          >
            {showLog ? 'Ẩn nhật ký' : `Xem chi tiết (${count})`}
          </button>
        )}
      </div>

      {justReturned && (
        <div className="vr-anticheat__alert" role="alert">
          <div>
            <strong>🚨 Phát hiện bạn vừa rời khỏi màn hình phòng thi ({justReturned.awaySecs} giây)!</strong>
            <p>
              Lý do: <em>{justReturned.reason}</em>. Tổng số lần vi phạm hiện tại: <strong>{justReturned.count} lần</strong>.
            </p>
          </div>
          <button
            type="button"
            className="vr-anticheat__dismiss"
            onClick={() => setJustReturned(null)}
          >
            Đã hiểu
          </button>
        </div>
      )}

      {showLog && hasViolations && (
        <ul className="vr-anticheat__log">
          {events.map((ev, idx) => (
            <li key={ev.id || idx}>
              <span className="vr-anticheat__log-num">Lần {count - idx}</span>
              <span className="vr-anticheat__log-time">🕒 {ev.time}</span>
              <span className="vr-anticheat__log-reason">{ev.reason}</span>
              {ev.awaySeconds != null && (
                <span className="vr-anticheat__log-dur">⏱ Rời {ev.awaySeconds} giây</span>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
