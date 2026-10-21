const MINUTE = 60000;
export function suggestExamStart(now = Date.now()) {
  // Picker has minute precision: round up so at least one full minute remains to prepare.
  const start = Math.ceil((now + MINUTE) / MINUTE) * MINUTE;
  return new Date(start + 7 * 3600000).toISOString().slice(0, 16);
}
export function buildExamSchedule(startValue, minutes, studentIds) {
  const start = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(startValue || '') ? new Date(`${startValue}:00+07:00`).getTime() : NaN;
  if (!Number.isFinite(start) || !Number.isInteger(minutes) || minutes < 1 || minutes > 180 || !studentIds.length) return { endTime: null, slots: [] };
  return {
    endTime: new Date(start + studentIds.length * minutes * MINUTE).toISOString(),
    slots: studentIds.map((studentId, i) => ({ studentId, startTime: new Date(start + i * minutes * MINUTE).toISOString(), endTime: new Date(start + (i + 1) * minutes * MINUTE).toISOString() })),
  };
}
