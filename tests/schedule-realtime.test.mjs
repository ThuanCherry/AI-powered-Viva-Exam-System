import test from 'node:test';
import assert from 'node:assert/strict';
import { watchSchedule } from '../src/lib/scheduleRealtime.js';

function fixture({ fail = false, deferred = false } = {}) {
  const window = new EventTarget(), document = new EventTarget();
  document.visibilityState = 'visible';
  const events = new Map(), updates = [], statuses = [];
  let tick, cleared = false, starts = 0, finish;
  const hub = {
    state: 'Disconnected',
    on: (name, fn) => events.set(name, fn), off: name => events.delete(name),
    onreconnecting: fn => events.set('reconnecting', fn),
    onreconnected: fn => events.set('reconnected', fn),
    onclose: fn => events.set('close', fn),
    async start() { starts++; if (deferred) await new Promise(resolve => { finish = resolve; }); if (fail) { fail = false; throw Error('offline'); } this.state = 'Connected'; },
    async stop() { this.state = 'Disconnected'; },
  };
  const stop = watchSchedule({ hub, window, document, refresh: value => updates.push(value), status: value => statuses.push(value),
    timers: { setInterval(fn) { tick = fn; return 1; }, clearInterval() { cleared = true; } } });
  return { hub, window, document, events, updates, statuses, stop, tick: () => tick(), finish: () => finish(), starts: () => starts, cleared: () => cleared };
}
const settle = () => new Promise(resolve => setImmediate(resolve));

test('SignalR schedule edit immediately refreshes student data without reloading the page', async () => {
  const f = fixture(); await settle(); f.updates.length = 0;
  const edit = { examId: 12, reason: 'updated' };
  f.events.get('ScheduleChanged')(edit);
  assert.deepEqual(f.updates, [edit]); f.stop();
});
test('missed events reconcile even if transport still reports Connected', async () => {
  const f = fixture(); await settle(); f.updates.length = 0;
  f.tick(); assert.equal(f.updates.length, 1); assert.equal(f.starts(), 1); f.stop();
});
test('failed initial connection retries and reconnect fetches current schedule', async () => {
  const f = fixture({ fail: true }); await settle();
  assert.match(f.statuses.at(-1), /dự phòng/);
  f.window.dispatchEvent(new Event('online')); await settle();
  assert.equal(f.starts(), 2); assert.match(f.statuses.at(-1), /Tự đồng bộ/);
  f.updates.length = 0; f.events.get('reconnected')(); assert.equal(f.updates.length, 1); f.stop();
});
test('returning to student tab refreshes immediately; unmounted connection cannot update it', async () => {
  const f = fixture(); await settle(); f.updates.length = 0;
  f.document.visibilityState = 'hidden'; f.tick(); assert.equal(f.updates.length, 0);
  f.document.visibilityState = 'visible'; f.document.dispatchEvent(new Event('visibilitychange')); assert.equal(f.updates.length, 1);
  f.stop(); f.window.dispatchEvent(new Event('focus')); f.tick();
  assert.equal(f.updates.length, 1); assert.ok(f.cleared());
});
test('StrictMode cleanup ignores pending start completion and duplicate start requests', async () => {
  const f = fixture({ deferred: true }); f.tick(); assert.equal(f.starts(), 1);
  f.updates.length = 0;
  f.stop(); f.finish(); await settle(); assert.equal(f.updates.length, 0); assert.equal(f.statuses.length, 0);
  assert.equal(f.hub.state, 'Disconnected');
});
