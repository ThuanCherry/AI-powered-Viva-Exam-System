/* global AudioWorkletProcessor, sampleRate, registerProcessor */
/* AudioWorklet: bounded PCM16 mono frames, no recorder container/codec or browser SpeechRecognition. */
class PcmCapture extends AudioWorkletProcessor {
  constructor() {
    super();
    this.frame = new Int16Array(1600);
    this.used = 0; this.phase = 0; this.sum = 0; this.count = 0; this.ended = false;
    this.port.onmessage = event => {
      if (event.data?.type === 'flush') {
        this.emit(); this.ended = true;
        this.port.postMessage({ type: 'flushed' });
      }
    };
  }
  emit() {
    if (!this.used) return;
    // DataView writes canonical little-endian PCM even on a different host architecture.
    const bytes = new ArrayBuffer(this.used * 2); const view = new DataView(bytes);
    for (let i = 0; i < this.used; i++) view.setInt16(i * 2, this.frame[i], true);
    this.port.postMessage({ type: 'audio', pcm: bytes }, [bytes]); this.used = 0;
  }
  process(inputs) {
    if (this.ended) return false;
    const channels = inputs[0];
    if (!channels?.length) return true;
    for (let i = 0; i < channels[0].length; i++) {
      let sample = 0;
      for (const channel of channels) sample += channel[i] || 0;
      this.sum += sample / channels.length; this.count++; this.phase += 16000;
      if (this.phase >= sampleRate) {
        this.phase -= sampleRate;
        const value = Math.max(-1, Math.min(1, this.sum / this.count)); this.sum = 0; this.count = 0;
        this.frame[this.used++] = Math.round(value * (value < 0 ? 32768 : 32767));
        if (this.used === this.frame.length) this.emit();
      }
    }
    return true;
  }
}
registerProcessor('aives-pcm16', PcmCapture);
