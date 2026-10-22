// Each mounted shell owns its connection, including React StrictMode remounts.
export function watchSchedule({ hub, refresh, status, window, document, timers = globalThis }) {
  let alive = true;
  let starting = false;
  const updated = event => { if (alive) refresh(event); };
  const connected = () => { if (alive) { status('SignalR · Tự đồng bộ'); updated(); } };
  const offline = () => { if (alive) status('Đang nối lại · Đồng bộ dự phòng'); };
  const start = async () => {
    if (!alive || starting || hub.state !== 'Disconnected') return;
    starting = true;
    try {
      await hub.start();
      if (!alive) { await hub.stop(); return; }
      connected();
    }
    catch { offline(); }
    finally { starting = false; }
  };
  const resume = () => { if (alive && document.visibilityState !== 'hidden') { updated(); void start(); } };
  hub.on('ScheduleChanged', updated);
  hub.onreconnecting(offline);
  hub.onreconnected(connected);
  hub.onclose(offline);
  void start();
  // Reconcile even when a socket appears connected but a notification was missed.
  const fallback = timers.setInterval(resume, 15000);
  window.addEventListener('focus', resume);
  window.addEventListener('online', resume);
  document.addEventListener('visibilitychange', resume);
  return () => {
    alive = false;
    timers.clearInterval(fallback);
    window.removeEventListener('focus', resume);
    window.removeEventListener('online', resume);
    document.removeEventListener('visibilitychange', resume);
    hub.off('ScheduleChanged', updated);
    void hub.stop().catch(() => {});
  };
}
