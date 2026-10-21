import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import { startLiveMicrophone } from '../src/audio/liveMicrophone.js';

function capture(rate) {
  let Processor;
  runInNewContext(readFileSync(new URL('../public/audio/pcm-worklet.js', import.meta.url), 'utf8'), {
    sampleRate: rate,
    AudioWorkletProcessor: class { constructor() { this.port = { postMessage: event => this.events.push(event) }; this.events = []; } },
    registerProcessor: (_, value) => { Processor = value; },
  });
  return new Processor();
}
for (const rate of [16000, 44100, 48000]) {
  test(`microphone PCM resamples ${rate}Hz to 16kHz without drift and flushes tail`, () => {
    const p = capture(rate); const samples = Math.floor(rate * 1.05);
    let offset = 0;
    while (offset < samples) { const size = Math.min(128, samples - offset); p.process([[new Float32Array(size).fill(0.5)]]); offset += size; }
    p.port.onmessage({ data: { type: 'flush' } });
    const frames = p.events.filter(e => e.type === 'audio');
    assert.equal(frames.reduce((n, f) => n + f.pcm.byteLength / 2, 0), Math.floor(samples * 16000 / rate));
    assert.ok(frames.every(f => f.pcm.byteLength <= 3200));
    assert.equal(new DataView(frames[0].pcm).getInt16(0, true), 16384);
    assert.equal(p.events.at(-1).type, 'flushed'); assert.equal(p.process([]), false);
  });
}
test('microphone stereo mixes to mono, clamps and emits little-endian PCM16', () => {
  const p = capture(16000);
  p.process([[new Float32Array([2, -2]), new Float32Array([2, -2])]]);
  p.port.onmessage({ data: { type: 'flush' } });
  const view = new DataView(p.events[0].pcm);
  assert.equal(view.getInt16(0, true), 32767); assert.equal(view.getInt16(2, true), -32768);
});

function fakeBrowser() {
  const previous = new Map(['navigator', 'window', 'AudioContext', 'AudioWorkletNode'].map(k => [k, Object.getOwnPropertyDescriptor(globalThis, k)]));
  let node, server, uploaded = [], inputComplete = false, disposed = 0, stopped = 0, closed = 0;
  const track = { stop() { stopped++; }, addEventListener() {} };
  const stream = { getTracks: () => [track], getAudioTracks: () => [track] };
  const inertNode = () => ({ connect() {}, disconnect() {} });
  class Context {
    audioWorklet = { addModule: async url => { assert.equal(url, '/audio/pcm-worklet.js'); } };
    async resume() {} async close() { closed++; }
    createMediaStreamSource() { return inertNode(); }
    createGain() { return { ...inertNode(), gain: { value: 1 } }; }
  }
  class Node {
    constructor() { node = this; this.port = { postMessage: () => this.port.onmessage({ data: { type: 'flushed' } }) }; }
    connect() {} disconnect() {}
    audio() { this.port.onmessage({ data: { type: 'audio', pcm: new ArrayBuffer(3200) } }); }
  }
  for (const [key, value] of Object.entries({ navigator: { mediaDevices: { getUserMedia: async () => stream } }, window: { AudioWorkletNode: Node }, AudioContext: Context, AudioWorkletNode: Node }))
    Object.defineProperty(globalThis, key, { value, configurable: true, writable: true });
  const hub = { state: 'Connected', stream(method, exam, turn, upload) {
    assert.equal(method, 'Transcribe'); assert.equal(exam, 1); assert.equal(turn, 'q-2');
    upload.subscribe({ next: value => uploaded.push(value), complete: () => { inputComplete = true; }, error() {} });
    return { subscribe(callbacks) { server = callbacks; return { dispose() { disposed++; } }; } };
  } };
  return { hub, get node() { return node; }, get server() { return server; }, get uploaded() { return uploaded; }, get complete() { return inputComplete; }, get disposed() { return disposed; }, get stopped() { return stopped; }, get closed() { return closed; }, restore() {
    for (const [key, descriptor] of previous) { if (descriptor) Object.defineProperty(globalThis, key, descriptor); else delete globalThis[key]; }
  } };
}

test('duplex microphone stop flushes frames and drains final transcript before completion', async () => {
  const f = fakeBrowser(); const states = [], events = [];
  try {
    const session = await startLiveMicrophone({ hub: f.hub, examId: 1, turnId: 'q-2', onState: s => states.push(s), onEvent: e => events.push(e), onError: message => assert.fail(message) });
    f.server.next({ type: 'ready', transcript: '' }); f.node.audio();
    assert.equal(Buffer.from(f.uploaded[0], 'base64').length, 3200);
    session.stop(); assert.ok(f.complete); assert.equal(states.at(-1), 'draining'); assert.equal(f.disposed, 0);
    f.server.next({ type: 'final', transcript: 'Tiếng Việt' }); f.server.complete();
    assert.equal(events.at(-1).transcript, 'Tiếng Việt'); assert.equal(states.at(-1), 'idle'); assert.ok(f.closed > 0 && f.stopped > 0);
  } finally { f.restore(); }
});

test('duplex microphone abort disposes stream and releases device', async () => {
  const f = fakeBrowser(); const controller = new AbortController();
  try {
    await startLiveMicrophone({ hub: f.hub, examId: 1, turnId: 'q-2', signal: controller.signal, onState() {}, onEvent() {}, onError: message => assert.fail(message) });
    f.server.next({ type: 'ready' }); controller.abort();
    assert.equal(f.disposed, 1); assert.ok(f.stopped > 0); assert.ok(f.complete);
  } finally { f.restore(); }
});

test('duplex microphone stops on unacknowledged backlog and preserves committed text', async () => {
  const f = fakeBrowser(); const errors = [], events = [];
  try {
    await startLiveMicrophone({ hub: f.hub, examId: 1, turnId: 'q-2', onState() {}, onEvent: e => events.push(e), onError: e => errors.push(e) });
    f.server.next({ type: 'ready' }); f.server.next({ type: 'final', transcript: 'Đã lưu' });
    for (let i = 0; i < 31; i++) f.node.audio();
    assert.equal(f.uploaded.length, 30); assert.equal(f.disposed, 1); assert.equal(errors.length, 1); assert.equal(events.at(-1).transcript, 'Đã lưu');
  } finally { f.restore(); }
});
