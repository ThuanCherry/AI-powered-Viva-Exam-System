import { useEffect, useRef, useState } from 'react';
import { api } from '../api/client.js';
import { getSession } from '../auth/session.js';
import { vnTime } from '../lib/time.js';

export const MAX_ALLOWED_VIOLATIONS = 3;

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

export function mergeViolationLogs(serverLogs = [], localLogs = []) {
  if (serverLogs && serverLogs.length > 0) {
    return serverLogs.map((l, idx) => ({
      id: l.logId || idx,
      time: l.timestamp ? vnTime(l.timestamp) : '',
      reason: l.reason || l.description || 'Chuyển Tab / Alt+Tab rời khỏi màn hình phòng thi',
      awaySeconds: null
    }));
  }
  return localLogs || [];
}

function playWarningTone(isTerminated = false) {
  try {
    const AudioCtx = window.AudioContext || window.webkitAudioContext;
    if (!AudioCtx) return;
    const ctx = new AudioCtx();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = isTerminated ? 'sawtooth' : 'triangle';
    osc.frequency.setValueAtTime(isTerminated ? 330 : 587.33, ctx.currentTime);
    osc.frequency.setValueAtTime(isTerminated ? 220 : 440, ctx.currentTime + 0.14);
    gain.gain.setValueAtTime(0.09, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.32);
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.start();
    osc.stop(ctx.currentTime + 0.32);
    setTimeout(() => ctx.close().catch(() => {}), 450);
  } catch {
    // Ignore audio context restriction
  }
}

export default function AntiCheatMonitor({
  examId,
  active,
  serverViolations = [],
  onViolationReported,
  onExceedLimit
}) {
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
  const terminatingRef = useRef(false);

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

      let newCount = 0;
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
        newCount = next.length;
        try {
          const json = JSON.stringify(next);
          localStorage.setItem(userKey, json);
          if (emailKey) localStorage.setItem(emailKey, json);
        } catch {
          // Ignore storage error
        }
        return next;
      });

      const exceeded = newCount > MAX_ALLOWED_VIOLATIONS;
      playWarningTone(exceeded);

      // Report to backend exam_logs and auto-terminate if count > 3
      (async () => {
        try {
          const nextState = await api.post(`/api/Exams/${examId}/attempt/focus-violation`, { reason });
          if (nextState) {
            onViolationReported?.(nextState);
            if (exceeded && nextState.status !== 'Completed' && !terminatingRef.current) {
              terminatingRef.current = true;
              onExceedLimit?.();
            }
          }
        } catch {
          if (exceeded && !terminatingRef.current) {
            terminatingRef.current = true;
            onExceedLimit?.();
          }
        }
      })();
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
      recordLeave('Alt+Tab / Rời khỏi cửa sổ phòng thi (Mất tiêu điểm màn hình)');
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
  }, [active, examId, userKey, emailKey, onViolationReported, onExceedLimit]);

  const displayEvents = events.length >= (serverViolations?.length || 0)
    ? events
    : mergeViolationLogs(serverViolations, events);
  const count = displayEvents.length;
  const hasViolations = count > 0;
  const remainingAllowed = Math.max(0, MAX_ALLOWED_VIOLATIONS - count);

  return (
    <div className={`vr-anticheat ${hasViolations ? 'is-warn' : 'is-safe'}`} role="region" aria-label="Giám sát phòng thi">
      <div className="vr-anticheat__bar">
        <div className="vr-anticheat__status">
          <span className={`vr-anticheat__dot ${hasViolations ? 'is-warn' : 'is-safe'}`} />
          {hasViolations ? (
            <span>
              <strong>
                ⚠️ Cảnh báo rời màn hình thi: {count} / {MAX_ALLOWED_VIOLATIONS} lần cho phép
              </strong>
              <span className="vr-anticheat__sub">
                {count > MAX_ALLOWED_VIOLATIONS
                  ? '— Đã vượt quá 3 lần quy định! Hệ thống đang tự động chấm dứt phiên thi.'
                  : remainingAllowed === 0
                  ? '— Đã chạm mức cảnh báo tối đa (3/3)! Thoát màn hình thêm 1 lần nữa sẽ bị chấm dứt bài thi ngay lập tức.'
                  : `— Còn tối đa ${remainingAllowed} lần cảnh báo trước khi hệ thống tự động chấm dứt bài thi.`}
              </span>
            </span>
          ) : (
            <span>
              <strong>🛡️ Giám sát phòng thi (Anti-Cheat): Đang bật (0 / {MAX_ALLOWED_VIOLATIONS} lần)</strong>
              <span className="vr-anticheat__sub">
                — Thoát màn hình hoặc bấm Alt+Tab quá {MAX_ALLOWED_VIOLATIONS} lần hệ thống sẽ tự động chấm dứt kỳ thi.
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
            {showLog ? 'Ẩn lịch sử' : `Lịch sử vi phạm (${count})`}
          </button>
        )}
      </div>

      {justReturned && (
        <div className="vr-anticheat__alert" role="alert">
          <div>
            <strong>
              🚨 Phát hiện rời khỏi màn hình phòng thi ({justReturned.awaySecs} giây) — Vi phạm lần {justReturned.count}/{MAX_ALLOWED_VIOLATIONS}!
            </strong>
            <p>
              Hành vi: <em>{justReturned.reason}</em>.{' '}
              {justReturned.count >= MAX_ALLOWED_VIOLATIONS
                ? 'CẢNH BÁO CUỐI CÙNG: Nếu thoát màn hình thêm 1 lần nữa (quá 3 lần), hệ thống sẽ tự động thu bài và chấm dứt kỳ thi!'
                : `Lưu ý: Thoát màn hình quá ${MAX_ALLOWED_VIOLATIONS} lần sẽ bị hệ thống tự động chấm dứt kỳ thi.`}
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
          {displayEvents.map((ev, idx) => (
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
