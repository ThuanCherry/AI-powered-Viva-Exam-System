// Each MediaRecorder blob is a complete recording (not a headerless timeslice).
// Legacy file-upload helper: decode locally to canonical PCM WAV. Live capture uses AudioWorklet.
export async function toWav(blob) {
  const context = new AudioContext();
  try {
    const decoded = await context.decodeAudioData(await blob.arrayBuffer());
    const offline = new OfflineAudioContext(1, Math.ceil(decoded.duration * 16000), 16000);
    const source = offline.createBufferSource(); source.buffer = decoded; source.connect(offline.destination); source.start();
    const mono = (await offline.startRendering()).getChannelData(0);
    const buffer = new ArrayBuffer(44 + mono.length * 2); const view = new DataView(buffer);
    const text = (offset, value) => [...value].forEach((c, i) => view.setUint8(offset + i, c.charCodeAt(0)));
    text(0, 'RIFF'); view.setUint32(4, buffer.byteLength - 8, true); text(8, 'WAVE'); text(12, 'fmt ');
    view.setUint32(16, 16, true); view.setUint16(20, 1, true); view.setUint16(22, 1, true);
    view.setUint32(24, 16000, true); view.setUint32(28, 32000, true); view.setUint16(32, 2, true); view.setUint16(34, 16, true);
    text(36, 'data'); view.setUint32(40, mono.length * 2, true);
    mono.forEach((s, i) => view.setInt16(44 + i * 2, Math.max(-1, Math.min(1, s)) * (s < 0 ? 32768 : 32767), true));
    return new Blob([buffer], { type: 'audio/wav' });
  } finally { await context.close(); }
}
